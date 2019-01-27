Direct3DHook for PS4 Remote Play

Original source from

http://spazzarama.com/2011/03/14/c-screen-capture-and-overlays-for-direct3d-9-10-and-11-using-api-hooks

This is a first prototype to capture the screen in real time of the PS4 remote play.

There were three projects which helped me to achieve this goal. First of all the open source DirectX-Hook and Shared Memory project from spazzarama and the program **API Monitor v2** for figuring out what is happening under the hood of the Remote Play app.

**Direct3DHook:**
https://github.com/spazzarama/Direct3DHook 
**Shared Memory**
https://github.com/spazzarama/SharedMemory
**API Monitor v2**
http://www.rohitab.com/apimonitor

The prototype is able to hook into the running PS4 Remote Play program and capture the screen. We can capture the remote play screen at a max resolution of 1280*720. At higher resolutions the IPC slows down the Remote Play process and made it laggy. If you want to process the captured images in real time it is recommended to process it directly in the hook and not sending it to your process via IPC as this has some performance impacts.
If you have any suggestions of how we can improve performance feel free to contribute so that we can improve the project.
