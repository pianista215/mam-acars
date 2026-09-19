# Changelog

## [1.1.1] - Upcoming
- Fixed header accumulation bug causing HTTP 400 errors on long flights (~6h+). Default headers (X-App-Name) were appended on every request instead of set once, eventually exceeding nginx's 8KB header limit.

## [1.1.0] - 2026-02-04
- Changed message for non existing FPL (404) from rest service.
- Added VSLast3Avg that calculate the average of the last 3 values readed of the Vertical Speed.

## [1.0.0] - 2026-01-31

- Initial version
