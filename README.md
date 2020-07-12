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

Navigate to the **media** directory in the source to see a rendered movie. You
could also see the video on [this
blog-post](https://devblog.pekspro.com/posts/create-a-time-lapse-video), were
you also could read more about how this project works and some thoughts what to
think about if you want to use this on Azure Functions.

![Thumbnail image](./media/thumbnail.png "Thumbnail image")
