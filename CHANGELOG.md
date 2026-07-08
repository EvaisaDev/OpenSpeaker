# Changelog

## [0.4.1] - 2026-07-08

_No notable changes._

## [0.4.0] - 2026-07-08

### Other
- Extension Rework ([`2aa7ca5`](https://github.com/EvaisaDev/OpenSpeaker/commit/2aa7ca5))
  - Extensions now have a 16ms update loop, extensions can define `OnUpdate()` as a callback.
  - Added chat.send(msg) API for extensions.
  - dofile/loadfile/require are now relative to the extension folder and relative to the root folder so both `dofile("foo.lua")` and `dofile("extensions/extension1/foo.lua")` should work.
  - Added keybind system, currently not used in the app but can be used by extensions.
  - Added keybind setting type for extensions.
  - Updated lua api docs

## [0.3.13] - 2026-07-06

### Other
- Fix crash when spamming chat ([`8e9adea`](https://github.com/EvaisaDev/OpenSpeaker/commit/8e9adea))
- fix ([`8a60caa`](https://github.com/EvaisaDev/OpenSpeaker/commit/8a60caa))
- Add kofi button to release notes ([`560dc88`](https://github.com/EvaisaDev/OpenSpeaker/commit/560dc88))
  Added a Ko-fi button
- Update README.md ([`28fea5c`](https://github.com/EvaisaDev/OpenSpeaker/commit/28fea5c))

## [0.3.12] - 2026-06-30

### Other
- Small update ([`28c7cb9`](https://github.com/EvaisaDev/OpenSpeaker/commit/28c7cb9))
  - Added ability to remove voice aliases with right click
  - Added check to prevent duplicate voice alias names.
- Update generate-changelog.ps1 ([`9e00953`](https://github.com/EvaisaDev/OpenSpeaker/commit/9e00953))

## [0.3.11] - 2026-06-26

### Documentation
- update changelog for 0.3.10 ([`5378928`](https://github.com/EvaisaDev/OpenSpeaker/commit/5378928))

### Other
- Update :3 ([`9366499`](https://github.com/EvaisaDev/OpenSpeaker/commit/9366499))
  - Added bot account login for if you want command replies to be sent by a bot account.
  - Fixed lead moderators not counting as mods. (thanks for the dogshit API twitch)
  - Added new menu for changing the built-in commands and their permissions.
  - Fixed command replies (probably)

## [0.3.10] - 2026-06-25

### Documentation
- update changelog for 0.3.9 ([`d9587f6`](https://github.com/EvaisaDev/OpenSpeaker/commit/d9587f6))

### Other
- Multiple small changes ([`42d4105`](https://github.com/EvaisaDev/OpenSpeaker/commit/42d4105))
  - Treat symbols as spaces setting now lets you define a list of symbols to split by.
  - Command prefixes are now detected in replies.
  - Commands are no longer spoken out loud if they were in a reply.
- make free the default model ([`d27097d`](https://github.com/EvaisaDev/OpenSpeaker/commit/d27097d))
  - Mash the free model the default for fish audio!

## [0.3.9] - 2026-06-25

### Documentation
- update changelog for 0.3.8 ([`23704a3`](https://github.com/EvaisaDev/OpenSpeaker/commit/23704a3))

### Other
- Small patch ([`05d0b98`](https://github.com/EvaisaDev/OpenSpeaker/commit/05d0b98))
  - Fixed the voice volume slider.
  - Allow voice slider to go up to 200% volume
  - Allow pressing delete or backspace to remove a voice from an alias

## [0.3.8] - 2026-06-24

### Documentation
- update changelog for 0.3.7 ([`3746093`](https://github.com/EvaisaDev/OpenSpeaker/commit/3746093))

### Other
- better fish tts handling! ([`8b91073`](https://github.com/EvaisaDev/OpenSpeaker/commit/8b91073))
  - Fish Audio voices are now handled differently
     - Instead of loading all the voices (impossible task), we do not preload any fish audio voices and instead have a separate dropdown for selecting a voice and searching their database.

## [0.3.7] - 2026-06-24

### Documentation
- update changelog for 0.3.6 ([`ed19672`](https://github.com/EvaisaDev/OpenSpeaker/commit/ed19672))

### Other
- null checks and shit ([`63ce66e`](https://github.com/EvaisaDev/OpenSpeaker/commit/63ce66e))
  - Added null checks to some of the TTS engines

## [0.3.6] - 2026-06-24

### Other
- finish removing fakeyou ([`cd64207`](https://github.com/EvaisaDev/OpenSpeaker/commit/cd64207))

## [0.3.4] - 2026-06-24

### Other
- fix resemble audio parsing. ([`0788b8f`](https://github.com/EvaisaDev/OpenSpeaker/commit/0788b8f))
  Fixed audio parsing for Resemble.AI (hopefully)

## [0.3.0] - 2026-06-23

### Other
- Fix changelog workflow ([`bb50d61`](https://github.com/EvaisaDev/OpenSpeaker/commit/bb50d61))
- Add changelogs ([`4a7ebf2`](https://github.com/EvaisaDev/OpenSpeaker/commit/4a7ebf2))
  - Added changelog in the app after updating.
- A bunch of fixes and shit ([`9bb027d`](https://github.com/EvaisaDev/OpenSpeaker/commit/9bb027d))
  - Simplified parts of the codebase down to be less of a mess.
  - ITtsEngine -> HttpTtsEngine
  - Fixed a few memory leaks.
  - Properly handle default TTS
  - Default alias selection menu is now a dropdown.
  - Improved cheer emote stripping.
  - Refactored a bunch of UI shit.
  Please tell me if there is issues thanks :)
- Add migration instructions to README ([`f73bf9c`](https://github.com/EvaisaDev/OpenSpeaker/commit/f73bf9c))
  Added migration instructions from speakerbot.
- Update README.md ([`4fc9537`](https://github.com/EvaisaDev/OpenSpeaker/commit/4fc9537))
- Reorganized the codebase to make more sense, because this shit was awful ([`62b4304`](https://github.com/EvaisaDev/OpenSpeaker/commit/62b4304))
- improvements ([`77f4b1f`](https://github.com/EvaisaDev/OpenSpeaker/commit/77f4b1f))
- WIP ([`7c22dbc`](https://github.com/EvaisaDev/OpenSpeaker/commit/7c22dbc))
- Initial commit ([`52f0c5d`](https://github.com/EvaisaDev/OpenSpeaker/commit/52f0c5d))












