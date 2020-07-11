# Time Lapse Creator

This is a simple demo project that shows how to create a time lapse video in
.NET. The project does this:

* Creates a title screen with fading text
* Adds a visible timestamp on each frame
* Adds background audio
* Compiles everything to a video with [ffmpeg](https://ffmpeg.org/)

The project is designed to run on Windows, but it should be trivial to make it
work on other platforms. Probably just some paths need to be adjusted.

[ffmpeg](https://ffmpeg.org/) is an open source tool. You need to download this
and copy **ffmpeg.exe** to the output directory of the project.

<video controls poster="./media/thumbnail.png">
  <source src="./media/sample.mp4" type="video/mp4">
</video>
