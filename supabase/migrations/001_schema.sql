-- Age of Arena — initial schema
-- Apply: supabase db push  (or paste in Supabase SQL editor)

-- ── Profiles ─────────────────────────────────────────────────────────────────
create table if not exists profiles (
  id          uuid primary key references auth.users(id) on delete cascade,
  username    text not null default 'Adventurer',
  avatar_url  text,
  created_at  timestamptz not null default now()
);

alter table profiles enable row level security;
create policy "profiles: own row" on profiles
  for all using (auth.uid() = id) with check (auth.uid() = id);
create policy "profiles: public read" on profiles
  for select using (true);

-- ── Matches ──────────────────────────────────────────────────────────────────
create table if not exists matches (
  id          uuid primary key default gen_random_uuid(),
  room_code   text not null,
  map_type    int  not null default 0,
  duration_s  int  not null default 0,
  ended_at    timestamptz not null default now(),
  replay_url  text    -- Supabase Storage path
);

alter table matches enable row level security;
-- Only service role (server) can insert matches
create policy "matches: server only insert" on matches
  for insert with check (false);   -- client can never insert
create policy "matches: public read" on matches
  for select using (true);

-- ── Match players ─────────────────────────────────────────────────────────────
create table if not exists match_players (
  id          bigserial primary key,
  match_id    uuid not null references matches(id) on delete cascade,
  player_id   uuid not null references profiles(id),
  team        int  not null,
  result      text not null check (result in ('win','loss','draw','void')),
  elo_before  int  not null default 1000,
  elo_after   int  not null default 1000
);

alter table match_players enable row level security;
create policy "match_players: server only insert" on match_players
  for insert with check (false);
create policy "match_players: public read" on match_players
  for select using (true);

-- ── Ratings ───────────────────────────────────────────────────────────────────
create table if not exists ratings (
  player_id   uuid primary key references profiles(id) on delete cascade,
  elo         int  not null default 1000,
  wins        int  not null default 0,
  losses      int  not null default 0,
  updated_at  timestamptz not null default now()
);

alter table ratings enable row level security;
create policy "ratings: server only write" on ratings
  for all with check (false);
create policy "ratings: public read" on ratings
  for select using (true);

-- ── Matchmaking queue ─────────────────────────────────────────────────────────
create table if not exists mm_queue (
  player_id   uuid primary key references profiles(id) on delete cascade,
  elo         int  not null default 1000,
  queued_at   timestamptz not null default now()
);

alter table mm_queue enable row level security;
create policy "mm_queue: own row" on mm_queue
  for all using (auth.uid() = player_id) with check (auth.uid() = player_id);

-- ── Lobbies ───────────────────────────────────────────────────────────────────
create table if not exists lobbies (
  room_code     text primary key,
  host_id       uuid not null references profiles(id),
  player_count  int  not null default 1,
  map_type      int  not null default 0,
  created_at    timestamptz not null default now()
);

alter table lobbies enable row level security;
create policy "lobbies: server upsert" on lobbies
  for insert with check (false);
create policy "lobbies: server delete" on lobbies
  for delete using (false);
create policy "lobbies: public read" on lobbies
  for select using (true);

-- ── Leaderboard view ─────────────────────────────────────────────────────────
create or replace view leaderboard as
  select p.id, p.username, p.avatar_url, r.elo, r.wins, r.losses,
         rank() over (order by r.elo desc) as rank
  from profiles p
  join ratings r on r.player_id = p.id
  where r.wins + r.losses >= 3  -- at least 3 ranked games
  order by r.elo desc
  limit 100;

-- ── apply_match_result (service-role only) ────────────────────────────────────
create or replace function apply_match_result(
  p_room_code   text,
  p_map_type    int,
  p_duration_s  int,
  p_replay_url  text,
  p_results     jsonb,   -- [{player_id, team, result}]
  p_secret      text     -- must match app.settings.match_secret
)
returns uuid
language plpgsql
security definer
as $$
declare
  v_secret text;
  v_match_id uuid;
  v_player record;
  v_elo_before int;
  v_elo_after  int;
  k constant int := 32;  -- ELO K-factor
  v_expected   float;
  v_score      float;
begin
  -- Validate secret
  select current_setting('app.match_secret', true) into v_secret;
  if v_secret is null or v_secret <> p_secret then
    raise exception 'UNAUTHORIZED';
  end if;

  -- Insert match
  insert into matches (room_code, map_type, duration_s, replay_url)
  values (p_room_code, p_map_type, p_duration_s, p_replay_url)
  returning id into v_match_id;

  -- Upsert ratings and record match_players
  for v_player in select * from jsonb_to_recordset(p_results) as x(player_id uuid, team int, result text)
  loop
    -- Ensure rating row exists
    insert into ratings (player_id) values (v_player.player_id)
      on conflict (player_id) do nothing;

    select elo into v_elo_before from ratings where player_id = v_player.player_id;

    -- Simple ELO vs median (1000 as baseline)
    v_expected := 1.0 / (1.0 + power(10.0, (1000 - v_elo_before) / 400.0));
    v_score := case v_player.result when 'win' then 1.0 when 'loss' then 0.0 else 0.5 end;
    v_elo_after := v_elo_before + round(k * (v_score - v_expected));

    update ratings set
      elo = v_elo_after,
      wins = wins + case when v_player.result = 'win' then 1 else 0 end,
      losses = losses + case when v_player.result = 'loss' then 1 else 0 end,
      updated_at = now()
    where player_id = v_player.player_id;

    insert into match_players (match_id, player_id, team, result, elo_before, elo_after)
    values (v_match_id, v_player.player_id, v_player.team, v_player.result, v_elo_before, v_elo_after);
  end loop;

  return v_match_id;
end;
$$;
