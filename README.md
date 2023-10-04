# MyBooru

My attempt at making a booru-style picture gallery website.
First "no-conveniences" iteration is to see how it is to build an app without such things as:

- Mappers
- ORM
- Request limiters
- \[some-nice-library\]

Conveniences will be added later in a new branch.

## Requirements

Grab the latest ffmpeg executables:

-**Windows**:
[From here](https://ffmpeg.org/download.html#build-windows)

-**Linux**:
[From here](https://johnvansickle.com/ffmpeg/)

Place them according to the graph below:

```
MyBooru
	└── ffmpeg
	       └── bin
                    ├── ffmpeg*
	            └── ffprobe*
```

##

_p.s. - API wasn't so well thought out in the beginning, so yeah..._
