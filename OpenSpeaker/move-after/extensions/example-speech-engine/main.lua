local API_BASE = "https://tts.example.com"

-- Required by every extension, gives OpenSpeaker information about the extension.
function Extension()
    return {
        id          = "example-tts",  -- unique extension id
        name        = "Example TTS",  -- display name
        description = "Example speech engine extension. Replace with your own API." -- Description that is displayed in the extensions list
    }
end

-- Registers a speech engine, makes OpenSpeaker call GetVoices/GenerateSpeech in this script
-- auth fields show up when the user adds the engine in the speech engines tab.
-- Optional: tell the importer to remap voices/configs from another engine to this one.
-- Useful when you were using a different engine as a workaround in old SpeakerBot.
-- RegisterMigrationHook({
--     remap_voices = { uberduck = "ext:my-engine" },
--     remap_config = {
--         uberduck = {
--             engine_id = "ext:my-engine",
--             fields = { api_key = "clientId", api_secret = "secretKey" }
--         }
--     }
-- })

RegisterSpeechEngine({
    id   = "example-tts",
    name = "Example TTS",
    auth = {
        {key = "api_key",    label = "API Key",    type = "string"},
        {key = "api_secret", label = "API Secret", type = "string"},
    }
})

-- Adds a settings panel in the Extensions tab
-- types: "text", "number", "checkbox", "dropdown" (dropdown needs options = {...})
RegisterSettings({
    {key = "voice_quality", label = "Voice Quality", type = "dropdown", options = {"standard", "premium"}, default = "standard"},
    {key = "speed",         label = "Speed",         type = "number",   default = 1.0},
    {key = "normalize",     label = "Normalize",     type = "checkbox", default = false},
    {key = "prefix",        label = "Text Prefix",   type = "text",     default = ""},
})

local function make_headers(engine_id)
    local auth = GetAuth(engine_id)  -- returns the auth values the user configured
    if not auth.api_key or auth.api_key == "" then return nil end
    local token = base64.encode(auth.api_key .. ":" .. auth.api_secret)  -- base64.decode also available
    return {
        ["Authorization"] = "Basic " .. token,
        ["Content-Type"]  = "application/json",
    }
end

function GetVoices(engine_id)
    local headers  = make_headers(engine_id)
    if not headers then return {} end

    -- GetSetting(key) grabs a single value, GetSettings() returns all of them as a table
    local quality = GetSetting("voice_quality")

    -- http.get(url, headers?) → {ok, status, body}
    local r = http.get(API_BASE .. "/voices?quality=" .. quality, headers)
    if not r.ok then
        log("GetVoices failed: HTTP " .. r.status)  -- log() writes to debug output
        return {}
    end

    local data   = json.parse(r.body)  -- json.stringify() is also available
    local voices = {}
    for i = 1, #data do
        local v = data[i]
        table.insert(voices, {
            id     = v.id,
            name   = v.name,
            locale = v.locale or "en-US",
            gender = v.gender or "Neutral",
        })
    end
    return voices
end

-- return values:
--   "https://..."                         // url, OpenSpeaker fetches it
--   {url = "https://...", format = "mp3"} // url with explicit format
--   {format = "mp3", data = "<base64>"}   // raw audio bytes as base64
--   nil                                   // failed, request is skipped
function GenerateSpeech(engine_id, voice_id, text, _alias)
    local headers  = make_headers(engine_id)
    if not headers then return nil end

    local settings = GetSettings()
    local body     = json.stringify({
        voice   = voice_id,
        text    = settings.prefix .. text,
        speed   = settings.speed,
        quality = settings.voice_quality,
    })

    -- http.post_bytes(url, body, contentType?, headers?) // returns {ok, status, data}  (data is base64)
    -- http.get_bytes(url, headers?) 
    -- http.post(url, body, contentType?, headers?) // returns {ok, status, body}  for text responses
    local r = http.post_bytes(API_BASE .. "/synthesize", body, "application/json", headers)
    if not r.ok then
        log("GenerateSpeech failed: HTTP " .. r.status)
        return nil
    end

    return { format = "mp3", data = r.data }  -- r.data is already base64
end

-- Called for every chat message before it's spoken, lets you modify or block it.
-- user fields: id, username, display_name, nickname, is_subscriber, is_mod, is_vip, is_broadcaster, is_regular, is_ignored, is_forced
-- return a string to replace the message, or return the original to pass through unchanged.
-- return "" or nil to block the message entirely.
function OnMessage(_, message)
    return message
end
