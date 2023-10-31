using BigCat.BigWorld;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgcodecsModule;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BigCatEditor.BigWorld
{
    public static class BigWorldLightmapBaker
    {
        /// <summary>
        /// 高精度Lightmap大小
        /// </summary>
        private const int c_hqLightmapSize = 1024;
        private const int c_hqLightmapResolution = 6;
        
        /// <summary>
        /// 中精度Lightmap大小
        /// </summary>
        private const int c_mqLightmapSize = 1024;
        private const int c_mqLightmapResolution = 4;
        
        /// <summary>
        /// 低精度Lightmap大小
        /// </summary>
        private const int c_lqLightmapSize = 1024;
        private const int c_lqLightmapResolution = 3;

        /// <summary>
        /// Terrain的Lightmap参数
        /// </summary>
        private const int c_terrainHqLightmapSize = 2048;
        private const int c_terrainMqLightmapSize = 512;
        private const int c_terrainLqLightmapSize = 512;
        private const int c_terrainLightmapResolution = 15;

        /// <summary>
        /// Lightmap贴图最终大小
        /// </summary>
        private const int c_objLightmapFinalSize = 1024;
        private const int c_terrainLightmapFinalSize = 512;
        
        /// <summary>
        /// 烘焙Lightmap
        /// </summary>
        public static void BakeLightmap(Dictionary<int, BigWorldBakerHelper.BigWorldBakeDataOfCell> sceneBakeData)
        {
            foreach (var pair in sceneBakeData)
            {
                BakeLightmapOfCell(pair.Key, pair.Value, LightmapQuality.High, sceneBakeData);
                BakeLightmapOfCell(pair.Key, pair.Value, LightmapQuality.Med, sceneBakeData);
                BakeLightmapOfCell(pair.Key, pair.Value, LightmapQuality.Low, sceneBakeData);
            }

            //刷新
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 烘焙Cell的Lightmap
        /// </summary>
        private static void BakeLightmapOfCell(int cellIndex, BigWorldBakerHelper.BigWorldBakeDataOfCell bakeData, LightmapQuality quality, Dictionary<int, BigWorldBakerHelper.BigWorldBakeDataOfCell> sceneBakeData)
        {
            int objLightmapSize, objLightmapResolution;
            int terrainLightmapSize, terrainLightmapResolution;
            switch (quality)
            {
                case LightmapQuality.High:
                    objLightmapSize = c_hqLightmapSize;
                    objLightmapResolution = c_hqLightmapResolution;
                    terrainLightmapSize = c_terrainHqLightmapSize;
                    terrainLightmapResolution = c_terrainLightmapResolution;
                    break;
                case LightmapQuality.Med:
                    objLightmapSize = c_mqLightmapSize;
                    objLightmapResolution = c_mqLightmapResolution;
                    terrainLightmapSize = c_terrainMqLightmapSize;
                    terrainLightmapResolution = c_terrainLightmapResolution;
                    break;
                default:
                    objLightmapSize = c_lqLightmapSize;
                    objLightmapResolution = c_lqLightmapResolution;
                    terrainLightmapSize = c_terrainLqLightmapSize;
                    terrainLightmapResolution = c_terrainLightmapResolution;
                    break;
            }
            
            //首先烘焙场景对象的Lightmap
            PreBakeLightmapOfSceneObj(bakeData, quality);
            BakeLightmap(objLightmapSize, objLightmapResolution);
            PostBakeLightmapOfSceneObj(cellIndex, bakeData, quality);
            
            //其次烘焙Terrain的Lightmap
            PreBakeLightmapOfTerrain(bakeData, quality);
            BakeLightmap(terrainLightmapSize, terrainLightmapResolution);
            PostBakeLightmapOfTerrain(cellIndex, bakeData, quality);
        }

        private static void PreBakeLightmapOfSceneObj(BigWorldBakerHelper.BigWorldBakeDataOfCell bakeData, LightmapQuality quality)
        {
            //首先清理场景对象的Lightmap参数
            ClearLightmap();
            ClearLightmapParams();

            //给场景对象设置新的Lightmap参数
            var lightmapParam = new LightmapParameters() { bakedLightmapTag = 100 };
            foreach (var bakeGroup in bakeData.bakeGroups)
            {
                foreach (var item in bakeGroup.items)
                {
                    SetLightmapParamsOfRenderer(item.renderer, quality, lightmapParam);
                }
            }
            
            //给Terrain设置Lightmap参数
            SetLightmapParamsOfTerrain(bakeData.terrain, LightmapQuality.None, null);
        }

        private static void PostBakeLightmapOfSceneObj(int cellIndex, BigWorldBakerHelper.BigWorldBakeDataOfCell bakeData, LightmapQuality quality)
        {
            //检查Lightmap输出路径
            BigWorldUtility.GetCellCoordinates(cellIndex, out var cellX, out var cellZ);
            var folder = $"Assets/Resources/BigWorld/{BigWorldBaker.worldName}/block/block_{cellX}_{cellZ}/";
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            //获取有效的Lightmap索引
            var lightmapIndices = new List<int>();
            foreach (var bakeGroup in bakeData.bakeGroups)
            {
                foreach (var item in bakeGroup.items)
                {
                    var lightmapIndex = item.renderer.lightmapIndex;
                    if (!lightmapIndices.Contains(lightmapIndex))
                    {
                        lightmapIndices.Add(lightmapIndex);
                    }

                    if (quality == LightmapQuality.High)
                    {
                        item.hqLightmapIndex = lightmapIndices.IndexOf(lightmapIndex);
                        item.hqLightmapScaleOffset = item.renderer.lightmapScaleOffset;
                    }
                    else if (quality == LightmapQuality.Med)
                    {
                        item.mqLightmapIndex = lightmapIndices.IndexOf(lightmapIndex);
                        item.mqLightmapScaleOffset = item.renderer.lightmapScaleOffset;
                    }
                    else
                    {
                        item.lqLightmapIndex = lightmapIndices.IndexOf(lightmapIndex);
                        item.lqLightmapScaleOffset = item.renderer.lightmapScaleOffset;
                    }
                }
            }

            //复制Lightmap
            var lightmapCopyPaths = new List<string>();
            var lightmapPrefix = quality == LightmapQuality.High ? "shq_lightmap" : (quality == LightmapQuality.Med ? "smq_lightmap" : "slq_lightmap");
            for (var i = 0; i < lightmapIndices.Count; ++i)
            {
                var lightmapIndex = lightmapIndices[i];
                var lightmapTexture = LightmapSettings.lightmaps[lightmapIndex].lightmapColor;
                var lightmapPath = AssetDatabase.GetAssetPath(lightmapTexture);
                var lightmapCopyPath = $"{folder}/{lightmapPrefix}_{i}.exr";
                lightmapCopyPaths.Add(lightmapCopyPath);
                AssetDatabase.CopyAsset(lightmapPath, lightmapCopyPath);
            }

            //刷新
            AssetDatabase.Refresh();

            //修改Lightmap设置
            AssetDatabase.StartAssetEditing();
            foreach (var path in lightmapCopyPaths)
            {
                SetLightmapSetting(path, c_objLightmapFinalSize);
            }
            AssetDatabase.StopAssetEditing();
        }

        private static void PreBakeLightmapOfTerrain(BigWorldBakerHelper.BigWorldBakeDataOfCell bakeData, LightmapQuality quality)
        {
            //首先清理场景对象的Lightmap参数
            ClearLightmap();
            ClearLightmapParams();

            //给场景对象设置新的Lightmap参数
            foreach (var bakeGroup in bakeData.bakeGroups)
            {
                foreach (var item in bakeGroup.items)
                {
                    SetLightmapParamsOfRenderer(item.renderer, LightmapQuality.None, null);
                }
            }
            
            //给Terrain设置Lightmap参数
            var lightmapParam = new LightmapParameters() { bakedLightmapTag = 100 };
            SetLightmapParamsOfTerrain(bakeData.terrain, quality, lightmapParam);
        }

        private static void PostBakeLightmapOfTerrain(int cellIndex, BigWorldBakerHelper.BigWorldBakeDataOfCell bakeData, LightmapQuality quality)
        {
            //检查Lightmap输出路径
            BigWorldUtility.GetCellCoordinates(cellIndex, out var cellX, out var cellZ);
            var folder = $"{BigWorldBaker.bakeTempOutputPath}/terrain_lm/temp_{cellX}_{cellZ}/";
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            //复制Lightmap
            var lightmapName = quality == LightmapQuality.High ? "thqlm" : (quality == LightmapQuality.Med ? "tmqlm" : "tlqlm");
            var lightmapIndex = bakeData.terrain.lightmapIndex;
            var lightmapTexture = LightmapSettings.lightmaps[lightmapIndex].lightmapColor;
            var lightmapPath = AssetDatabase.GetAssetPath(lightmapTexture);
            File.Copy(lightmapPath, $"{folder}/{lightmapName}.exr", true);
        }

        #region Bake Utility

        /// <summary>
        /// 烘焙Lightmap
        /// </summary>
        /// <param name="atlasSize">Lightmap贴图尺寸</param>
        /// <param name="bakeResolution">烘焙精度</param>
        private static void BakeLightmap(int atlasSize, float bakeResolution)
        {
            Lightmapping.lightingSettings.lightmapMaxSize = atlasSize;
            Lightmapping.lightingSettings.lightmapResolution = bakeResolution;
            Lightmapping.lightingSettings.directionalityMode = LightmapsMode.NonDirectional;
            Lightmapping.lightingSettings.lightmapCompression = LightmapCompression.None;
            Lightmapping.Bake();
        }
        
        /// <summary>
        /// 清理Lightmap
        /// </summary>
        private static void ClearLightmap()
        {
            Lightmapping.Clear();
        }

        /// <summary>
        /// 清理场景对象的Lightmap参数
        /// </summary>
        private static void ClearLightmapParams()
        {
            for (var i = 0; i < SceneManager.sceneCount; ++i)
            {
                var scene = SceneManager.GetSceneAt(i);
                foreach (var root in scene.GetRootGameObjects())
                {
                    var children = root.GetComponentsInChildren<Transform>();
                    foreach (var child in children)
                    {
                        GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
                    }
                }
            }
        }

        /// <summary>
        /// 设置Renderer的Lightmap参数
        /// </summary>
        private static void SetLightmapParamsOfRenderer(MeshRenderer renderer, LightmapQuality quality, LightmapParameters lightmapParam)
        {
            renderer.scaleInLightmap = quality switch
            {
                LightmapQuality.High => 1.5f,
                LightmapQuality.Med => 1.0f,
                LightmapQuality.Low => 0.5f,
                _ => 0
            };
            renderer.stitchLightmapSeams = false;

            if (lightmapParam != null)
            {
                var so = new SerializedObject(renderer);
                var sp = so.FindProperty("m_LightmapParameters");
                sp.objectReferenceValue = lightmapParam;
                so.ApplyModifiedProperties();
            }
                    
            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.ContributeGI);
        }

        /// <summary>
        /// 给Terrain设置Lightmap烘焙参数
        /// </summary>
        private static void SetLightmapParamsOfTerrain(Terrain terrain, LightmapQuality quality, LightmapParameters lightmapParam)
        {
            var so = new SerializedObject(terrain);

            var sp1 = so.FindProperty("m_ScaleInLightmap");
            if (quality == LightmapQuality.None)
            {
                sp1.floatValue = 0.0f;
            }
            else
            {
                sp1.floatValue = 1.0f;

                var sp2 = so.FindProperty("m_LightmapParameters");
                sp2.objectReferenceValue = lightmapParam;
            }

            so.ApplyModifiedProperties();

            terrain.shadowCastingMode = ShadowCastingMode.TwoSided;
            GameObjectUtility.SetStaticEditorFlags(terrain.gameObject, StaticEditorFlags.ContributeGI);
        }

        /// <summary>
        /// 设置Lightmap贴图的设置
        /// </summary>
        /// <param name="lightmapPath">lightmap路径，Assets开始</param>
        /// <param name="lightmapSize">lightmap贴图尺寸</param>
        private static void SetLightmapSetting(string lightmapPath, int lightmapSize)
        {
            var importer = AssetImporter.GetAtPath(lightmapPath) as TextureImporter;
            if (importer != null)
            {
                importer.isReadable = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = lightmapSize;

                var androidSetting = importer.GetPlatformTextureSettings("Android");
                androidSetting.overridden = true;
                androidSetting.format = TextureImporterFormat.ASTC_6x6;
                androidSetting.maxTextureSize = lightmapSize;
                importer.SetPlatformTextureSettings(androidSetting);

                var iosSetting = importer.GetPlatformTextureSettings("iPhone");
                iosSetting.overridden = true;
                iosSetting.format = TextureImporterFormat.ASTC_6x6;
                iosSetting.maxTextureSize = lightmapSize;
                importer.SetPlatformTextureSettings(iosSetting);

                var pcSetting = importer.GetPlatformTextureSettings("Standalone");
                pcSetting.overridden = true;
                pcSetting.format = TextureImporterFormat.DXT5;
                pcSetting.maxTextureSize = lightmapSize;
                importer.SetPlatformTextureSettings(pcSetting);
                    
                importer.SaveAndReimport();
            }
        }

        #endregion

        #region Postprocess Terrain Lightmap
        public static void PostprocessLightmap()
        {
            //后处理Terrain的LightMap
            PostprocessLqTerrainLightmap();
            PostprocessMqTerrainLightmap();
            PostprocessHqTerrainLightmap();

            //拷贝到Resources
            var terrainLmFolder = $"{BigWorldBaker.bakeTempOutputPath}/terrain_lm/";
            if (Directory.Exists(terrainLmFolder))
            {
                var path = $"Assets/Resources/BigWorld/{BigWorldBaker.worldName}/terrain_lm";
                var folders = Directory.GetDirectories(terrainLmFolder, "chunk_*", SearchOption.TopDirectoryOnly);
                foreach (var folder in folders)
                {
                    var folderName = Path.GetFileNameWithoutExtension(folder);
                    BigWorldBakerHelper.CopyFolder(folder, $"{path}/{folderName}");
                }
                AssetDatabase.Refresh();

                //修改Lightmap参数
                AssetDatabase.StartAssetEditing();
                var hqLightmapPaths = Directory.GetFiles(path, "thqlm_*.exr", SearchOption.AllDirectories);
                foreach (var lightmapPath in hqLightmapPaths)
                {
                    SetLightmapSetting(lightmapPath.Replace(Application.dataPath, "Assets"), c_terrainLightmapFinalSize);
                }

                var mqLightmapPaths = Directory.GetFiles(path, "tmqlm_*.exr", SearchOption.AllDirectories);
                foreach (var lightmapPath in mqLightmapPaths)
                {
                    SetLightmapSetting(lightmapPath.Replace(Application.dataPath, "Assets"), c_terrainLightmapFinalSize);
                }

                var lqLightmapPaths = Directory.GetFiles(path, "tlqlm_*.exr", SearchOption.AllDirectories);
                foreach (var lightmapPath in lqLightmapPaths)
                {
                    SetLightmapSetting(lightmapPath.Replace(Application.dataPath, "Assets"), c_terrainLightmapFinalSize);
                }
                AssetDatabase.StopAssetEditing();
            }
        }

        /// <summary>
        /// 合并低精度的Terrain的Lightmap
        /// </summary>
        private static void PostprocessLqTerrainLightmap()
        {
            var terrainLmFolder = $"{BigWorldBaker.bakeTempOutputPath}/terrain_lm/";
            if (!Directory.Exists(terrainLmFolder))
            {
                return;
            }

            var chunkLightmaps = new Dictionary<int, Texture2D>();

            var folders = Directory.GetDirectories(terrainLmFolder, "temp_*", SearchOption.TopDirectoryOnly);
            foreach (var folder in folders)
            {
                var folderName = Path.GetFileNameWithoutExtension(folder);
                var folderSs = folderName.Split('_');
                var blockX = Convert.ToInt32(folderSs[1]);
                var blockZ = Convert.ToInt32(folderSs[2]);
                var chunkX = blockX / 4;
                var chunkZ = blockZ / 4;
                var chunkIndex = BigWorldUtility.GetCellIndex(chunkX, chunkZ);
                if (!chunkLightmaps.TryGetValue(chunkIndex, out var chunkLightmap))
                {
                    var textureSize = c_terrainLqLightmapSize * 4;
                    chunkLightmap = new Texture2D(textureSize, textureSize, TextureFormat.RGBAFloat, -1, true);
                    chunkLightmaps.Add(chunkIndex, chunkLightmap);
                }

                using (var blockLightmap = Imgcodecs.imread($"{folder}/tlqlm.exr", Imgcodecs.IMREAD_UNCHANGED))
                {
                    var offsetX = blockX % 4 * c_terrainLqLightmapSize;
                    var offsetZ = blockZ % 4 * c_terrainLqLightmapSize;

                    var width = blockLightmap.width();
                    var height = blockLightmap.height();
                    var lightmapColor = new float[blockLightmap.channels()];
                    for (var x = 0; x < width; ++x)
                    {
                        for (var y = 0; y < height; ++y)
                        {
                            blockLightmap.get(height - 1 - y, x, lightmapColor);
                            chunkLightmap.SetPixel(offsetX + x, offsetZ + y, new Color(lightmapColor[2], lightmapColor[1], lightmapColor[0]));
                        }
                    }
                }
            }

            foreach (var pair in chunkLightmaps)
            {
                BigWorldUtility.GetCellCoordinates(pair.Key, out var chunkX, out var chunkZ);
                var chunkFolder = $"{terrainLmFolder}/chunk_{chunkX}_{chunkZ}";
                if (!Directory.Exists(chunkFolder))
                {
                    Directory.CreateDirectory(chunkFolder);
                }

                using (var mat = ConvertTextureToMat(pair.Value))
                {
                    Imgcodecs.imwrite($"{chunkFolder}/tlqlm.exr", mat);
                }
            }
        }

        /// <summary>
        /// 移动中精度的Terrain的Lightmap
        /// </summary>
        private static void PostprocessMqTerrainLightmap()
        {
            var terrainLmFolder = $"{BigWorldBaker.bakeTempOutputPath}/terrain_lm/";
            if (!Directory.Exists(terrainLmFolder))
            {
                return;
            }

            var folders = Directory.GetDirectories(terrainLmFolder, "temp_*", SearchOption.TopDirectoryOnly);
            foreach (var folder in folders)
            {
                var folderName = Path.GetFileNameWithoutExtension(folder);
                var folderSs = folderName.Split('_');
                var blockX = Convert.ToInt32(folderSs[1]);
                var blockZ = Convert.ToInt32(folderSs[2]);
                var chunkX = blockX / 4;
                var chunkZ = blockZ / 4;
                var chunkFolder = $"{terrainLmFolder}/chunk_{chunkX}_{chunkZ}";
                if (!Directory.Exists(chunkFolder))
                {
                    Directory.CreateDirectory(chunkFolder);
                }
                File.Copy($"{folder}/tmqlm.exr", $"{chunkFolder}/tmqlm_{blockX}_{blockZ}.exr", true);
            }
        }

        /// <summary>
        /// 移动中精度的Terrain的Lightmap
        /// </summary>
        private static void PostprocessHqTerrainLightmap()
        {
            var terrainLmFolder = $"{BigWorldBaker.bakeTempOutputPath}/terrain_lm/";
            if (!Directory.Exists(terrainLmFolder))
            {
                return;
            }

            var folders = Directory.GetDirectories(terrainLmFolder, "temp_*", SearchOption.TopDirectoryOnly);
            foreach (var folder in folders)
            {
                var folderName = Path.GetFileNameWithoutExtension(folder);
                var folderSs = folderName.Split('_');
                var blockX = Convert.ToInt32(folderSs[1]);
                var blockZ = Convert.ToInt32(folderSs[2]);
                var chunkX = blockX / 4;
                var chunkZ = blockZ / 4;
                var chunkFolder = $"{terrainLmFolder}/chunk_{chunkX}_{chunkZ}";
                if (!Directory.Exists(chunkFolder))
                {
                    Directory.CreateDirectory(chunkFolder);
                }

                var stepLightmapSize = c_terrainHqLightmapSize / 4;
                using (var blockLightmap = Imgcodecs.imread($"{folder}/thqlm.exr", Imgcodecs.IMREAD_UNCHANGED))
                {
                    var lightmapColor = new float[blockLightmap.channels()];
                    var stepStartX = blockX * 4;
                    var stepStartZ = blockZ * 4;
                    for (var m = 0; m < 4; ++m)
                    {
                        for (var n = 0; n < 4; ++n)
                        {
                            var texture = new Texture2D(stepLightmapSize, stepLightmapSize, TextureFormat.RGBAFloat, -1, true);

                            for (int x = 0; x < stepLightmapSize; ++x)
                            {
                                for (int y = 0; y < stepLightmapSize; ++y)
                                {
                                    blockLightmap.get(blockLightmap.height() - 1 - (y + n * stepLightmapSize), (x + m * stepLightmapSize), lightmapColor);
                                    texture.SetPixel(x, y, new Color(lightmapColor[2], lightmapColor[1], lightmapColor[0]));
                                }
                            }

                            using (var mat = ConvertTextureToMat(texture))
                            {
                                var stepX = stepStartX + m;
                                var stepZ = stepStartZ + n;
                                Imgcodecs.imwrite($"{chunkFolder}/thqlm_{stepX}_{stepZ}.exr", mat);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 将Texture2D转成Mat
        /// </summary>
        /// <returns></returns>
        private static Mat ConvertTextureToMat(Texture2D tex)
        {
            var mat = new Mat(tex.width, tex.height, CvType.CV_32FC3);
            int width = tex.width;
            int height = tex.height;
            float[] bgr = new float[3];
            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; ++x)
                {
                    Color c = tex.GetPixel(x, y);
                    bgr[0] = c.b;
                    bgr[1] = c.g;
                    bgr[2] = c.r;
                    mat.put(height - 1 - y, x, bgr);
                }
            }

            return mat;
        }
        #endregion
    }   
}