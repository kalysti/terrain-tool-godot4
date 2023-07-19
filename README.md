# GODOT 4 3D Terrain Editor (C# Plugin) (GPU Based)

Tool is written for prototyping in c# but will be converted to c++ for the godot core.

## Needs a master godot 4 build with mono!

## Read at first

The terrain is splited in a patch grid (int x, int y) to make the terrain on the fly extensible.
Each patch has a constant number of 16 chunks. 
Each chunk have a chunk size of power of 2 - 1.

## Install

1. Copy all files to addons/TerrainPlugin
2. Enable it
3. Have fun :-)

## Screenshots

![Construction of Terrain Editor](https://i.ibb.co/3T9sp5q/terraintool.png)
![Construction of Terrain Editor 2](https://i.ibb.co/8YS53tD/uploadtool2.png)

## Currently supported

- Sculpting
	- Holes
	- Flatten
	- Noise
	- Sculpting
	- Smoothing
- Painting
	- On each splatmap channel
- Brushes
	- Smooth, Linear, Spherical, Tip by given radius, strength and fallof
- Importing and exporting heightmaps and splatmaps for 16bit raw images (industrial default)

## Import formats
Currently the tool supports 4 kinds of importing heightmaps.

- R16
	- Importing heightmaps by a 16 Bit (L8) Format (Industrial deault)
- RGBA8_Half
	- Importing heightmaps by a RGBA 8Bit Image which splitted the height on the RED and GREEN Channel (16bit) 
- RGBA8_NORMAL
	- Is the format which the editor is using (RED = 8 bit height, GREEN = +8 extrabit, BLUE = normal.x, ALPHA = normal.z)
- RGB_FULL
	- Importing heightmaps by a RGB 8Bit Image which is using 24bit (Mapbox used that one)

## Mapbox Support
- Adding a custom node "TerrainMapBox3D" which can downloading datas directly from mapbox and draw it as a terrain

## Internal format

- Heightmap
	- Format: RGBA8
	- Channel R: 8 bit Height
	- Channel G: +8 extrabit Heigt
	- Channel B: Normal X
	- Channel A: Normal Z
	- Normal.ONE = HOLE
- Splatmaps
	- Format: RGBA8
	- Channel R to A = Layer 0 - 3

## Visual Shader Nodes

 - TerrainGeneratorNode for Vertex Shader
	 - Node to generation vertex, normals, binormals and tangents for the fragment shader based on the heightmap
 - TerrainAntiTilingNode for Fragment Shader
	 - Effecting the uv rotation of a TerrainTexturePack to reduce the tiling by small randomly rotation
 - TerrainDepthBlendingNode for Fragment Shader
	 - Mixing two TerrainTexturePacks by a given mix value in depth
 - TerrainTexturePack and TerrainTextureUnpack for Fragment and Vertex Shader
	 - Returns a Transform (mat4) by using Albedo, Normal, Roughness, AO and Heightmap.
 - TerrainDefaultMaterial for Fragment Shader
	 - Returns the default material for the editor
 - TerrainSplatmapData for Fragment Shader
	 - Returns the value of a given splatmap layer


## Todo
- Setup automatic lod level by camera distance
- Create blank terrain shader template by a button (create terrain shader)
- Move to native
