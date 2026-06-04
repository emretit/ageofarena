using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// N9.i18n: Static key→string localization table (TR + EN).
/// Switch language at runtime via <see cref="SetLanguage"/>.
/// Usage: <c>Loc.Get("key")</c> — falls back to the key itself if missing.
/// </summary>
public static class Loc
{
    public enum Lang { TR, EN }

    static Lang _lang = Lang.TR;
    public static Lang Language => _lang;

    public static void SetLanguage(Lang lang)
    {
        _lang = lang;
        PlayerPrefs.SetString("Loc.Lang", lang.ToString());
    }

    public static void LoadSaved()
    {
        string saved = PlayerPrefs.GetString("Loc.Lang", "TR");
        _lang = saved == "EN" ? Lang.EN : Lang.TR;
    }

    public static string Get(string key)
    {
        var dict = _lang == Lang.EN ? _en : _tr;
        return dict.TryGetValue(key, out var v) ? v : key;
    }

    // ── Resource labels ──────────────────────────────────────────────────────
    static readonly Dictionary<string, string> _tr = new()
    {
        // Resource bar
        { "res.food",   "Yiyecek" },
        { "res.wood",   "Odun"    },
        { "res.gold",   "Altın"   },
        { "res.stone",  "Taş"     },
        { "res.pop",    "Nüfus"   },
        { "res.relic",  "Emanet"  },

        // Age names
        { "age.dark",     "Karanlık"     },
        { "age.feudal",   "Derebeylik"   },
        { "age.castle",   "Kale"         },
        { "age.imperial", "İmparatorluk" },

        // Difficulty
        { "diff.easy",     "Kolay"     },
        { "diff.moderate", "Orta"      },
        { "diff.normal",   "Normal"    },
        { "diff.hard",     "Zor"       },
        { "diff.insane",   "Acımasız"  },
        { "diff.extreme",  "Efsanevi"  },

        // Game mode
        { "mode.random",       "Rastgele"       },
        { "mode.deathmatch",   "Ölüm Maçı"      },
        { "mode.regicide",     "Kral Avı"       },
        { "mode.nomad",        "Göçebe"         },
        { "mode.empirewars",   "İmparatorluk"   },
        { "mode.koth",         "Tepenin Kralı"  },
        { "mode.suddendeath",  "Ani Ölüm"       },
        { "mode.treaty",       "Antlaşma"       },
        { "mode.turbo",        "Turbo"          },

        // HUD labels
        { "hud.age",        "Çağ"          },
        { "hud.difficulty", "Zorluk"       },
        { "hud.civ",        "Medeniyet"    },
        { "hud.time",       "Süre"         },
        { "hud.idleWorker", "Boşta köylü" },
        { "hud.garrison",   "Garnizon"     },
        { "hud.producing",  "Üretim (iptal için tıkla):" },
        { "hud.researching","Araştırılıyor" },
        { "hud.speed.stop", "Dur"          },

        // Stance names
        { "stance.aggressive",  "Saldırgan"   },
        { "stance.defensive",   "Savunmacı"   },
        { "stance.standground", "Yerinde Dur" },
        { "stance.noattack",    "Saldırma"    },
        { "stance.label",       "Duruş"       },

        // Unit names
        { "unit.Villager",       "Köylü"          },
        { "unit.Militia",        "Asker"          },
        { "unit.Archer",         "Okçu"           },
        { "unit.Cavalry",        "Süvari"         },
        { "unit.Trebuchet",      "Mancınık"       },
        { "unit.Scout",          "Gözcü"          },
        { "unit.Medic",          "Şifacı"         },
        { "unit.Spearman",       "Mızrakçı"       },
        { "unit.Longbowman",     "Uzun Yaylı"     },
        { "unit.Galley",         "Gemi"           },
        { "unit.Skirmisher",     "Avcı"           },
        { "unit.ManAtArms",      "Ağır Asker"     },
        { "unit.CavalryArcher",  "Atlı Okçu"      },
        { "unit.Camel",          "Deve Süvari"     },
        { "unit.Champion",       "Şampiyon"       },
        { "unit.Paladin",        "Şövalye"        },
        { "unit.Halberdier",     "Mızrakçı Usta"  },
        { "unit.Arbalest",       "Arbalet"        },
        { "unit.EliteSkirmisher","Seçkin Avcı"    },
        { "unit.Hussar",         "Hüsar"          },
        { "unit.BatteringRam",   "Koçbaşı"        },
        { "unit.Mangonel",       "Manganel"       },
        { "unit.Scorpion",       "Akrep"          },
        { "unit.Monk",           "Rahip"          },
        { "unit.Petard",         "Petard"         },
        { "unit.Merchant",       "Tüccar"         },
        { "unit.Missionary",     "Misyoner"       },
        { "unit.Eagle",          "Kartal Savaşçı" },
        { "unit.FireShip",       "Ateş Gemisi"    },
        { "unit.WarGalley",      "Savaş Gemisi"   },
        { "unit.Galleon",        "Galeyon"        },
        { "unit.Cannon",         "Top"            },
        { "unit.DemoShip",       "Demo Gemisi"    },
        { "unit.Fisherman",      "Balıkçı"        },
        { "unit.FishTrap",       "Balık Tuzağı"   },
        { "unit.King",           "Kral"           },
        { "unit.Mangudai",       "Mangudai"       },
        { "unit.WarElephant",    "Savaş Fili"     },
        { "unit.ThrowingAxeman", "Balta Atan"     },
        { "unit.Cataphract",     "Katafrak"       },
        { "unit.Berserk",        "Berserk"        },
        { "unit.Mameluke",       "Memlük"         },
        { "unit.WoadRaider",     "Woad Akıncısı"  },
        { "unit.ChuKoNu",        "Chu Ko Nu"      },
        { "unit.Huskarl",        "Huskarl"        },
        { "unit.Janissary",      "Yeniçeri"       },

        // Command card
        { "cmd.stop",        "Dur"            },
        { "cmd.attackMove",  "Saldır-Yürü"   },
        { "cmd.stance",      "Duruş"          },
        { "cmd.garrison",    "Garnizona Gir"  },
        { "cmd.ungarrison",  "Garnizon Boşalt"},
        { "cmd.repair",      "Onar"           },
        { "cmd.buildMenu",   "İnşa Menüsü"   },
        { "cmd.delete",      "Sil"            },

        // Tooltips
        { "tip.stop",       "Tüm emirleri bırak ve dur."                    },
        { "tip.attackMove", "Bir noktaya ilerle; yoldaki düşmana saldır."   },
        { "tip.stance",     "Saldırı duruşunu değiştir (Agresif/Savunma/Sabit/Pasif)." },
        { "tip.garrison",   "Seçili birimi binaya yerleştir."               },
        { "tip.ungarrison", "İçerideki birimleri dışarı çıkar."             },
        { "tip.repair",     "Seçili binayı onar."                           },
        { "tip.buildMenu",  "İnşa etmek için yapı seç."                     },
        { "tip.delete",     "Seçili birimi/binayı sil."                     },

        // Market
        { "market.sellFood",  "Yiyecek Sat"  },
        { "market.sellWood",  "Odun Sat"     },
        { "market.sellStone", "Taş Sat"      },
        { "market.buyFood",   "Yiyecek Al"   },
        { "market.buyTip",    "Altın vererek yiyecek satın al." },
        { "market.sellTip",   "Bu kaynağı altına çevir."        },

        // Pause menu
        { "pause.resume",   "Devam"          },
        { "pause.hotkeys",  "Tuşlar"         },
        { "pause.resign",   "Teslim Ol"      },
        { "pause.restart",  "Yeniden Başlat" },
        { "pause.fogOn",    "Sis Aç"         },
        { "pause.fogOff",   "Sis Kapat"      },

        // Hotkey panel
        { "hk.title",         "Tuş Atamaları"    },
        { "hk.stop",          "Durdur"           },
        { "hk.attackMove",    "Saldır-Yürü"     },
        { "hk.stance",        "Duruş Değiştir"  },
        { "hk.garrison",      "Garnizona Gir"   },
        { "hk.ungarrison",    "Garnizon Boşalt" },
        { "hk.diplomacy",     "Diplomasi"       },
        { "hk.selectIdle",    "Boşta İşçi Seç"  },
        { "hk.buildMenu",     "İnşa Menüsü"    },
        { "hk.repair",        "Onar"            },
        { "hk.resetAll",      "Varsayılana Dön" },
        { "hk.back",          "Geri"            },
        { "hk.listening",     "Bir tuşa bas..." },

        // Game over
        { "over.win",    "ZAFER!"     },
        { "over.lose",   "YENİLDİN"   },
        { "over.restart","[R] Yeniden Başlat" },
        { "over.score",  "Skor"       },
        { "over.army",   "Ordu"       },
        { "over.worker", "Köylü"      },
        { "over.build",  "Bina"       },
        { "over.gold",   "Altın"      },
        { "over.age",    "Çağ"        },
        { "over.team.0", "Sen"        },
        { "over.team.1", "Kırmızı"   },
        { "over.team.2", "Yeşil"     },
        { "over.team.3", "Sarı"      },

        // Victory countdown
        { "vc.wonder",    "Anıt zaferi"  },
        { "vc.relic",     "Kalıntı zaferi" },
        { "vc.koth",      "KotH"          },

        // Misc
        { "misc.buildSelect", "Bina yapmak için seç" },
        { "misc.buildRepair", "Onar/sil" },
        { "misc.garrisonFmt", "Garnizon {0}/{1}" },
        { "misc.stanceFmt",   "Duruş: {0}  [Q]"  },
    };

    static readonly Dictionary<string, string> _en = new()
    {
        // Resource bar
        { "res.food",   "Food"   },
        { "res.wood",   "Wood"   },
        { "res.gold",   "Gold"   },
        { "res.stone",  "Stone"  },
        { "res.pop",    "Pop"    },
        { "res.relic",  "Relic"  },

        // Age names
        { "age.dark",     "Dark Age"        },
        { "age.feudal",   "Feudal Age"      },
        { "age.castle",   "Castle Age"      },
        { "age.imperial", "Imperial Age"    },

        // Difficulty
        { "diff.easy",     "Easy"      },
        { "diff.moderate", "Moderate"  },
        { "diff.normal",   "Normal"    },
        { "diff.hard",     "Hard"      },
        { "diff.insane",   "Insane"    },
        { "diff.extreme",  "Extreme"   },

        // Game mode
        { "mode.random",      "Random"         },
        { "mode.deathmatch",  "Deathmatch"     },
        { "mode.regicide",    "Regicide"       },
        { "mode.nomad",       "Nomad"          },
        { "mode.empirewars",  "Empire Wars"    },
        { "mode.koth",        "King of the Hill" },
        { "mode.suddendeath", "Sudden Death"   },
        { "mode.treaty",      "Treaty"         },
        { "mode.turbo",       "Turbo"          },

        // HUD labels
        { "hud.age",        "Age"        },
        { "hud.difficulty", "Difficulty" },
        { "hud.civ",        "Civilization" },
        { "hud.time",       "Time"       },
        { "hud.idleWorker", "Idle villager" },
        { "hud.garrison",   "Garrison"   },
        { "hud.producing",  "Training (click to cancel):" },
        { "hud.researching","Researching" },
        { "hud.speed.stop", "Stop"       },

        // Stance names
        { "stance.aggressive",  "Aggressive"   },
        { "stance.defensive",   "Defensive"    },
        { "stance.standground", "Stand Ground" },
        { "stance.noattack",    "No Attack"    },
        { "stance.label",       "Stance"       },

        // Unit names
        { "unit.Villager",       "Villager"        },
        { "unit.Militia",        "Militia"         },
        { "unit.Archer",         "Archer"          },
        { "unit.Cavalry",        "Knight"          },
        { "unit.Trebuchet",      "Trebuchet"       },
        { "unit.Scout",          "Scout"           },
        { "unit.Medic",          "Monk"            },
        { "unit.Spearman",       "Spearman"        },
        { "unit.Longbowman",     "Longbowman"      },
        { "unit.Galley",         "Galley"          },
        { "unit.Skirmisher",     "Skirmisher"      },
        { "unit.ManAtArms",      "Man-at-Arms"     },
        { "unit.CavalryArcher",  "Cavalry Archer"  },
        { "unit.Camel",          "Camel Rider"     },
        { "unit.Champion",       "Champion"        },
        { "unit.Paladin",        "Paladin"         },
        { "unit.Halberdier",     "Halberdier"      },
        { "unit.Arbalest",       "Arbalest"        },
        { "unit.EliteSkirmisher","Elite Skirmisher" },
        { "unit.Hussar",         "Hussar"          },
        { "unit.BatteringRam",   "Battering Ram"   },
        { "unit.Mangonel",       "Mangonel"        },
        { "unit.Scorpion",       "Scorpion"        },
        { "unit.Monk",           "Monk"            },
        { "unit.Petard",         "Petard"          },
        { "unit.Merchant",       "Merchant"        },
        { "unit.Missionary",     "Missionary"      },
        { "unit.Eagle",          "Eagle Warrior"   },
        { "unit.FireShip",       "Fire Ship"       },
        { "unit.WarGalley",      "War Galley"      },
        { "unit.Galleon",        "Galleon"         },
        { "unit.Cannon",         "Cannon Galleon"  },
        { "unit.DemoShip",       "Demo Ship"       },
        { "unit.Fisherman",      "Fisherman"       },
        { "unit.FishTrap",       "Fish Trap"       },
        { "unit.King",           "King"            },
        { "unit.Mangudai",       "Mangudai"        },
        { "unit.WarElephant",    "War Elephant"    },
        { "unit.ThrowingAxeman", "Throwing Axeman" },
        { "unit.Cataphract",     "Cataphract"      },
        { "unit.Berserk",        "Berserk"         },
        { "unit.Mameluke",       "Mameluke"        },
        { "unit.WoadRaider",     "Woad Raider"     },
        { "unit.ChuKoNu",        "Chu Ko Nu"       },
        { "unit.Huskarl",        "Huskarl"         },
        { "unit.Janissary",      "Janissary"       },

        // Command card
        { "cmd.stop",        "Stop"           },
        { "cmd.attackMove",  "Attack Move"    },
        { "cmd.stance",      "Stance"         },
        { "cmd.garrison",    "Garrison"       },
        { "cmd.ungarrison",  "Ungarrison"     },
        { "cmd.repair",      "Repair"         },
        { "cmd.buildMenu",   "Build"          },
        { "cmd.delete",      "Delete"         },

        // Tooltips
        { "tip.stop",       "Stop all orders and stand still."          },
        { "tip.attackMove", "Move to a point; attack enemies on the way." },
        { "tip.stance",     "Cycle attack stance (Aggressive/Defensive/Stand Ground/No Attack)." },
        { "tip.garrison",   "Put the selected unit inside a building."  },
        { "tip.ungarrison", "Release all units from the building."      },
        { "tip.repair",     "Repair the selected building."             },
        { "tip.buildMenu",  "Select a building to construct."           },
        { "tip.delete",     "Delete the selected unit/building."        },

        // Market
        { "market.sellFood",  "Sell Food"   },
        { "market.sellWood",  "Sell Wood"   },
        { "market.sellStone", "Sell Stone"  },
        { "market.buyFood",   "Buy Food"    },
        { "market.buyTip",    "Spend gold to buy food."           },
        { "market.sellTip",   "Convert this resource to gold."    },

        // Pause menu
        { "pause.resume",   "Resume"   },
        { "pause.hotkeys",  "Hotkeys"  },
        { "pause.resign",   "Resign"   },
        { "pause.restart",  "Restart"  },
        { "pause.fogOn",    "Enable Fog" },
        { "pause.fogOff",   "Disable Fog" },

        // Hotkey panel
        { "hk.title",         "Hotkey Bindings"   },
        { "hk.stop",          "Stop"              },
        { "hk.attackMove",    "Attack Move"       },
        { "hk.stance",        "Cycle Stance"      },
        { "hk.garrison",      "Garrison"          },
        { "hk.ungarrison",    "Ungarrison"        },
        { "hk.diplomacy",     "Diplomacy"         },
        { "hk.selectIdle",    "Select Idle Villager" },
        { "hk.buildMenu",     "Build Menu"        },
        { "hk.repair",        "Repair"            },
        { "hk.resetAll",      "Reset All"         },
        { "hk.back",          "Back"              },
        { "hk.listening",     "Press a key..."    },

        // Game over
        { "over.win",    "VICTORY!"   },
        { "over.lose",   "DEFEATED"   },
        { "over.restart","[R] Restart" },
        { "over.score",  "Score"      },
        { "over.army",   "Army"       },
        { "over.worker", "Villager"   },
        { "over.build",  "Building"   },
        { "over.gold",   "Gold"       },
        { "over.age",    "Age"        },
        { "over.team.0", "You"        },
        { "over.team.1", "Red"        },
        { "over.team.2", "Green"      },
        { "over.team.3", "Yellow"     },

        // Victory countdown
        { "vc.wonder",    "Wonder victory"  },
        { "vc.relic",     "Relic victory"   },
        { "vc.koth",      "KotH"            },

        // Misc
        { "misc.buildSelect", "Select a building to construct" },
        { "misc.buildRepair", "Repair / Delete" },
        { "misc.garrisonFmt", "Garrison {0}/{1}" },
        { "misc.stanceFmt",   "Stance: {0}  [Q]" },
    };
}
