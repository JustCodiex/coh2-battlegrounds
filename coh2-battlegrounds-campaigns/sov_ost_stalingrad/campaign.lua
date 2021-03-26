campaign = {
    display = {
        fe_name = "campaign_name",
        fe_desc = "campaign_desc",
        fe_location = "campaign_location",
        theatre = "east",
        modes = {
            "singleplayer",
            "cooperative",
            "competitive",
        },
        start = "german",
        max_players = 8,
        start_date = { year = 1942, month = 9, day = 13 },
        end_date = { year = 1943, month = 2, day = 2 },
        turn_time = { months = 0.25 }, -- 1/4 = 1 week...
    },
    weather = {
        winter = {
            start_date = { year = 1942, month = 11, day = 1 },
            end_date = { year = 1943, month = 1, day = 1},
        },
        allowed_atmospheres = {
            s = {

            },
            w = {

            },
        },
    },
    armies = {
        ["soviet"] = {
            fe_army_name = "soviet_army",
            fe_army_desc = "soviet_army_desc",
            army_file = "soviets.lua",
            army_colour = {
                255, 0, 0
            },
            min_players = 1,
            max_players = 4,
        },
        ["german"] = {
            fe_army_name = "german_army",
            fe_army_desc = "german_army_desc",
            army_file = "germans.lua",
            army_colour = {
                0, 255, 0
            },
            min_players = 1,
            max_players = 4,
        }
    },
    resources = {
        {
            type = MAP,
            file = "map.png",
        },
        {
            type = LOCALE,
            lang = "ALL",
            file = "locale.loc",
        },
        {
            type = SCRIPT,
            file = "victory.lua",
        },
        {
            type = SCRIPT,
            file = "events.lua",
        },
        {
            type = GFX,
            file = "gfx.lua",
        },
    }
}
