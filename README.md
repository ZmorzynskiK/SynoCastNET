# SynoCastNET
YouTube audio only podcast downloader for Synology NAS. Pure .NET, no docker!

## Introduction
This is simple, .NET 9 console app just to get audio files from YouTube videos.
I use it to get YouTube podcasts.
Uses `YoutubeExplode` library.

## Features
1. NO DOCKER REQUIRED - just simple one executable with one simple config file.
2. NO NODE JS or PYTHON REQUIRED
3. Suitable for Synology NAS (Linux ARM64 version).
4. You can specify the url of the channel or playlist, specify language and file format you want to use (mp4 is handled widely) and how many files you want to get.
5. Old downloaded files are automatically deleted.
6. Oh, did I mention NO DOCKER IS REQUIRED?!
7. Great for using with Synology Drive to have automatically audio files downloaded and placed on your phone.

## How to use with Synology NAS
1. Check `config.json` file to see sample config.
1. Just place the `SynoCastNET` linux arm64 executable somewhere (ie. in `/volume1/Apps`) on synology and place `config.json` alongside.
2. Create new folder for downloaded data on synology, ie. `/SynoCastNET`
3. In `config.json` specify `outputDirectory` as `/volume1/SynoCastNET`
4. Run `./SynoCastNET` just to check if the files are downloaded to this output folder.
5. Set `/volume1/SynoCastNET` as `Team folder` in Synology Drive Admin and setup user as normal
6. On your client device (PC or smartphone) setup synchronization task to get files from `/volume1/SynoCastNET`
7. On synology setup task scheduler to run `SynoCastNET` periodically, ie. every night with custom script as:
```
cd /volume1/Apps/SynoCastNET
./SynoCastNET
```
