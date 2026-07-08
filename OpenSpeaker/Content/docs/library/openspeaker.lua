---@meta

---@class openspeaker.ExtensionMeta
---@field id string Unique id. Stored everywhere as "ext:<id>".
---@field name string Display name shown in the UI.
---@field description? string

---@class openspeaker.AuthField
---@field key string
---@field label? string Defaults to a prettified key.
---@field type? "string" Only "string" is supported.

---@alias openspeaker.SettingType "text"|"number"|"checkbox"|"dropdown"|"keybind"

---@class openspeaker.SettingField
---@field key string
---@field label? string Defaults to a prettified key.
---@field type? openspeaker.SettingType Defaults to "text".
---@field default? string|number|boolean
---@field options? string[] Required for "dropdown", ignored otherwise.

---@class openspeaker.EngineDef
---@field id string Raw id. OpenSpeaker registers it as "ext:<id>".
---@field name? string Defaults to the id.
---@field auth? openspeaker.AuthField[]

---@class openspeaker.Voice
---@field id string
---@field name string
---@field locale? string
---@field gender? string

---@class openspeaker.ChatUser
---@field id string
---@field username string
---@field display_name string
---@field nickname string
---@field is_subscriber boolean
---@field is_mod boolean
---@field is_vip boolean
---@field is_broadcaster boolean
---@field is_regular boolean
---@field is_ignored boolean
---@field is_forced boolean

---@class openspeaker.SpeechResult
---@field url? string OpenSpeaker fetches this url.
---@field data? string Raw audio bytes as base64.
---@field async_job? string Job id from http.get_bytes_async or http.post_bytes_async.
---@field format? "mp3"|"wav" Anything that is not "wav" is decoded as mp3.

---@class openspeaker.MigrationEngine
---@field engine_id string
---@field fields table<string, string> Your field name to the SpeakerBot source field name.

---@class openspeaker.MigrationHook
---@field remap_voices? table<string, string>
---@field remap_config? table<string, openspeaker.MigrationEngine>

---@class openspeaker.HttpResult
---@field ok boolean False on a network failure.
---@field status integer 0 on a network failure.
---@field body string The error message when ok is false.

---@class openspeaker.HttpBytesResult
---@field ok boolean False on a network failure.
---@field status integer 0 on a network failure.
---@field data string Base64 audio bytes, or "" when ok is false.

---Registers a speech engine, shown under Speech Engines > Add.
---@param def openspeaker.EngineDef
function RegisterSpeechEngine(def) end

---Adds a settings panel under the Extensions tab.
---@param fields openspeaker.SettingField[]
function RegisterSettings(fields) end

---Tells the SpeakerBot importer to remap another engine's voices and config to yours.
---@param hook openspeaker.MigrationHook
function RegisterMigrationHook(hook) end

---Returns the auth values the user entered for an engine.
---@param engine_id string The raw id, without the "ext:" prefix.
---@return table<string, string>
function GetAuth(engine_id) end

---Returns a single setting value. Checkbox fields are booleans, number fields are numbers,
---everything else is a string. Unset fields fall back to their default.
---@param key string
---@return string|number|boolean|nil
function GetSetting(key) end

---Returns every registered setting, typed the same way GetSetting types them.
---@return table<string, string|number|boolean>
function GetSettings() end

---Reserved. Currently always returns an empty table.
---@return table
function GetVoiceSettings() end

---Writes to the debug log.
---@param msg string
function log(msg) end

chat = {}

---Sends a message to the connected Twitch chat. No-op until chat is connected.
---@param msg string
function chat.send(msg) end

keybind = {}

---True while every key in the combo is down.
---@param key string A "keybind" setting key, or a literal spec like "F8" or "Ctrl+Shift+K".
---@return boolean
function keybind.held(key) end

---True on the tick the combo became fully held. Taps shorter than one tick are still reported.
---@param key string A "keybind" setting key, or a literal spec like "F8" or "Ctrl+Shift+K".
---@return boolean
function keybind.pressed(key) end

---True on the tick the combo stopped being fully held.
---@param key string A "keybind" setting key, or a literal spec like "F8" or "Ctrl+Shift+K".
---@return boolean
function keybind.released(key) end

http = {}

---@param url string
---@param headers? table<string, string>
---@return openspeaker.HttpResult
function http.get(url, headers) end

---@param url string
---@param body string
---@param contentType? string Defaults to "application/json".
---@param headers? table<string, string>
---@return openspeaker.HttpResult
function http.post(url, body, contentType, headers) end

---@param url string
---@param headers? table<string, string>
---@return openspeaker.HttpBytesResult
function http.get_bytes(url, headers) end

---@param url string
---@param body string
---@param contentType? string Defaults to "application/json".
---@param headers? table<string, string>
---@return openspeaker.HttpBytesResult
function http.post_bytes(url, body, contentType, headers) end

---Starts a download without blocking. Return the job id as async_job from GenerateSpeech.
---@param url string
---@param headers? table<string, string>
---@return string job_id
function http.get_bytes_async(url, headers) end

---Starts a download without blocking. Return the job id as async_job from GenerateSpeech.
---@param url string
---@param body string
---@param contentType? string Defaults to "application/json".
---@param headers? table<string, string>
---@return string job_id
function http.post_bytes_async(url, body, contentType, headers) end

json = {}

---@param str string
---@return any value nil if the string is not valid json.
function json.parse(str) end

---A table with a 1..n array part becomes a json array, any other table becomes a json object.
---An empty table serializes as an object.
---@param val any
---@return string json "null" if the value cannot be serialized.
function json.stringify(val) end

base64 = {}

---@param str string
---@return string
function base64.encode(str) end

---@param str string
---@return string decoded "" if the input is not valid base64.
function base64.decode(str) end

---Required. Called once on load to get extension metadata.
---@return openspeaker.ExtensionMeta
function Extension() end

---Required if you registered a speech engine. Return {} if auth is missing or wrong.
---@param engine_id string The raw id, without the "ext:" prefix.
---@return openspeaker.Voice[]
function GetVoices(engine_id) end

---Required if you registered a speech engine.
---Return a url string, a openspeaker.SpeechResult table, or nil to skip the request.
---@param engine_id string The raw id, without the "ext:" prefix.
---@param voice_id string
---@param text string
---@param alias nil Reserved, currently always nil.
---@return string|openspeaker.SpeechResult|nil
function GenerateSpeech(engine_id, voice_id, text, alias) end

---Optional. Observes every chat message, including commands, ignored prefixes, messages from users
---without permission and messages dropped by the cooldown. Fires before any of that filtering, on
---the raw unsanitized message. Cannot change or block anything, the return value is ignored.
---@param user openspeaker.ChatUser
---@param message string
function OnChat(user, message) end

---Optional. Intercepts chat messages before they get spoken. Only runs on messages that survived
---commands, ignored prefixes, permissions, cooldown and sanitizing. Use OnChat to see everything.
---Return a string to replace the message, or "" to block it. Returning nil passes the original
---message through unchanged, it does not block. Several extensions chain in load order.
---@param user openspeaker.ChatUser
---@param message string
---@return string|nil
function OnMessage(user, message) end

---Optional. Called about 60 times per second. Only runs if your extension defines OnUpdate or
---registers a keybind setting. A tick is skipped if the previous one is still running.
function OnUpdate() end
