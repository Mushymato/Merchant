# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.0] - 2026-01-12

### Added

- Robo-shopkeeper now supports auto-restocking too
- The session report menu has been improved and you can now browse all past reports
- Merchant upgrades now appear as powers, and each have dedicated icons in the upgrade menu

### Changed

- Minigame no-longer snaps the camera to the top left of map when you start. You can still pan the camera.

### Fixed
- (HxW Add-On) actually make these work oops

## [1.1.1] - 2026-01-10

### Added

- Shop can now open until 1:30am, added a fail to open shop message
- New GSQ `mushymato.Merchant_ITEM_MATCHES_THEME` which check if an item matches the theme

### Fixed

- Soft-lock at 1am oops
- Context tags are now trimmed

## [1.1.0] - 2026-01-07

### Added

- Android compatibility (thanks to [Ekyso](https://github.com/Mushymato/Merchant/pull/1))
- New robo shopkeeper that you can buy, for auto-selling things overnight
- The cash register action now takes args to add boosts, which work just like existing theme boosts
- Add new option haggle wait to change time to pause after haggling
- Allow changing sound cues via content patcher.

### Changed

- Adjusted price of auto-restock upgrade to 25000
- Adjusted give up rate to 1/2, up from 1/3
- Reduced pointer slowing effect from themed boost

### Fixed

- Incompatibility with custom backpack framework
- Soft-lock if player somehow dies during shopkeeping

## [1.0.0] - 2026-01-06

### Added

- Initial release
