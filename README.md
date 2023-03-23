# Contraband
Tooling for talking to .NET web APIs from VRChat using video protocols.  

## Inspiration
Kudos to [pixel-proxy](https://gitlab.com/anfaux/pixel-proxy) and [udon-video-decoder](https://github.com/Roliga/udon-video-decoder) for the basis.  VRChat doesn't let you invoke web requests from your world code but some clever individuals found a way around that by ~~abusing~~ being a little creative with the SDK.  Contraband aims to improve the efficiency of the foundation laid in those projects and improve the developer experience for creators wanting to integrate external calls into their worlds.  

## Key Features
* **Direct integration into ASP.NET Core request pipeline**: Contraband plugs right into your API server's middleware pipeline to allow you to easily add video transmission on new and existing web APIs.  Simply register the tools in your application and any request made with `Content-Type: video/mp4` in the request headers or `video=true` in the query parameters will be formatted as a video for consumption in VRC.
  ```csharp
  builder.Services.AddControllers(options =>
  {
      options.AddContrabandFormatters();
  });
  builder.Services.AddContraband();
  app.UseContraband();
  ```
* **Binary Serialization**: Contraband is designed to make use of binary serializers to make the data transmitted across it more efficient.
* **Dynamic Resolution**: Contraband automatically adjusts output video dimensions based on the size of the data payload.
* **Data Integrity Checks**: Contraband payloads include an 8-byte header containing size and hash information to verify the integrity of recieved data on the client end.
  
## Progress
At this stage, server-side packages have been coded and will undergo more testing before public release.  Client code, namely the deserializer, are in the process of being implemented.  The current plan is to implement a MessagePack deserializer for U#, but this is still in preliminary investigative stages.  

## Future Performance Enhancements
This is a non-exhaustive list of adjustments that could be made in the future to improve the performance of Contraband.

## Encoding
* **Make Use of Color Data**: Right now data is encoded as a series of black and white pixels.  Colors cannot be relied on at a per-pixel level due to the inherent 4:2:0 chroma subsampling present in MP4 video.  A more sophisticated approach to image encoding could potentially store additional data using chrominance at a 2x2 pixel grid level.  
* **Decrease Header Size**: Size data in header currently spans 4 bytes but this could be reduced to 2 bytes worth of data given the resolution limitations that are imposed.  

### Server
* **Array Copies**: Several byte array copies occur throughout the process of generating, converting, and serving the image.  These could be cleaned up to reduce memory operations.
* **File System IO**: Currently the generated image is written to disk to be passed to FFmpeg for encoding and then the resulting file read back from the file system.  In the future data could be piped directly into and out of FFmpeg to avoid the disk IO