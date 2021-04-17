# GODOT 4 Terrain Plugin (GPU Based)

Currently under development. Tool is written for prototyping in c# but will be converted to c++ for the godot core.

## Needs to build godot 4 with a working heightmap collider
 - https://github.com/godotengine/godot/pull/47347 (for godot physics)
 - or https://github.com/godotengine/godot/pull/47508 (bullet physics)

## Construction

![Construction of Terrain Editor](https://i.ibb.co/3T9sp5q/terraintool.png)
![Construction of Terrain Editor 2](https://i.ibb.co/8YS53tD/uploadtool2.png)

## Todo
- Setup automatic lod level by camera distance
- Create blank terrain shader template by a button (create terrain shader)
- Code cleanup and loop reduction

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
- Importing and exporting heightmaps and splatmaps

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
