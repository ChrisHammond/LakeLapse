# Lake Lapse Bot by Oliver Hine

A simple tool for capturing daily timelapses of the sunrise and publishing it online on various social networks.

## Features
- capture images from sunrise to sunset
- output timelapse video with ffmpeg
- automatic selection of the "best" sunrise photo
- montage creation every 7 days
- daily video posting to twitter

## Demo
You can view the output at [twitter](https://twitter.com/LakeLapse), [instagram](https://www.instagram.com/lakelapse), and [facebook](https://www.facebook.com/groups/lakeontario)

## Requirements
- any camera with a guest url for direct jpeg access, we are currently using the g3 flex from ubiquiti
- twitter account with elevated api access
- raspberry pi, or Windows Subsystem for Linux to process video
- include the hashtag #lakelapse in your social media posts with videos produced by this application

## Usage
- edit `LakeLapseBot.dll.config` with your configuration details
- ffmpeg in your system path or extracted alongside LakeLapse
- run `./LakeLapse --verbose` or `dotnet LakeLapse.dll --verbose`
  - additional commands
  - `./LakeLapse --help`
  - `./LakeLapse --date "01-01-2022" --montage --verbose`
