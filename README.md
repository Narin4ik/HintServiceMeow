## Language / 语言 / Língua / Язык
### [English](Docs/English/README.md)
### [中文](Docs/SimplifiedChinese/README.md)
### [Português](Docs/Brazilian//README.md)
### [Русский](Docs/Russian/README.md)

---

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT) [![Discord](https://img.shields.io/badge/Discord-Join-5865F2?logo=discord&logoColor=white)](https://discord.gg/H3TACT3Buh) [![GitHub Release](https://img.shields.io/github/v/release/MeowServer/HintServiceMeow)](https://github.com/MeowServer/HintServiceMeow/releases)

## Introduction
**HintServiceMeow (HSM)** is a SCP: Secret Laboratory framework that allows plugins to display text on a selected position on a player's screen. 

---

## Installation

To install this plugin, follow these steps:

1. Go to the [Release Page](https://github.com/MeowServer/HintServiceMeow/releases) and download the latest `HintServiceMeow.dll`. Then, paste it into your plugin folder.
2. If you are using **LabAPI** (the default API), place `Harmony.dll` into the **dependencies** folder.
3. Restart your server.
4. Adjust config based on your need.
5. Restart your server again to apply changes of config.

---

## Documentation

- [Адаптация экрана и сохранение регистра (HSM 5.5.2)](Docs/Russian/ScreenAdaptation.md)

Here are some useful resources to get you started:

- [Getting Started](/Docs/English/GettingStarted.md)
- [Core Features](/Docs/English/CoreFeatures.md)
- [Change Log](/Docs/English/CHANGELOG.md)

---

## FAQ

### 1. Why doesn't the plugin work?
- Ensure that **HintServiceMeow** is installed correctly.
- Check if any other plugins conflict with **HintServiceMeow**.
- Review any errors that occur when activating plugins.

### 2. Why do hints overlap with each other?
- This might happen when multiple plugins place hints in the same position. You can adjust the UI position in each plugin's configuration file. 
- If a plugin doesn't allow you to change the position via its config file, please contact the plugin's author for assistance.

---

## Contributors

Thank you to everyone who has contributed to HintServiceMeow! 
Your pull requests, bug reports, and suggestions help keep this project running.

- [@Someone](https://github.com/Someone-193) - For adding code style check.
- [XLittleLeft](https://github.com/XLittleLeft) - For adding LabAPI support.
- [Firething](https://github.com/Firething) - For adding Portuguese translation.
