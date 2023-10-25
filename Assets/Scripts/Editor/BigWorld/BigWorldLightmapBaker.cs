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
                    terrainLightmapSize = 1024;
                    terrainLightmapResolution = 15;
                    break;
                case LightmapQuality.Med:
                    objLightmapSize = c_mqLightmapSize;
                    objLightmapResolution = c_mqLightmapResolution;
                    terrainLightmapSize = 1024;
                    terrainLightmapResolution = 15;
                    break;
                default:
                    objLightmapSize = c_lqLightmapSize;
                    objLightmapResolution = c_lqLightmapResolution;
                    terrainLightmapSize = 1024;
                    terrainLightmapResolution = 15;
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
            BigWorldBakerHelper.GetCellCoordinate(cellIndex, out var cellX, out var cellZ);
            var folder = $"Assets/Resources/BigWorld/{BigWorldBaker.worldName}/cell_{cellX}_{cellZ}/";
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
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importer.isReadable = true;
                    importer.mipmapEnabled = false;

                    var androidSetting = importer.GetPlatformTextureSettings("Android");
                    androidSetting.overridden = true;
                    androidSetting.format = TextureImporterFormat.ASTC_6x6;
                    importer.SetPlatformTextureSettings(androidSetting);
                    
                    importer.SaveAndReimport();
                }
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
            BigWorldBakerHelper.GetCellCoordinate(cellIndex, out var cellX, out var cellZ);
            var folder = $"Assets/Resources/BigWorld/{BigWorldBaker.worldName}/cell_{cellX}_{cellZ}/";
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            //复制Lightmap
            var lightmapPrefix = quality == LightmapQuality.High ? "thq_lightmap" : (quality == LightmapQuality.Med ? "tmq_lightmap" : "tlq_lightmap");
            var lightmapIndex = bakeData.terrain.lightmapIndex;
            var lightmapTexture = LightmapSettings.lightmaps[lightmapIndex].lightmapColor;
            var lightmapPath = AssetDatabase.GetAssetPath(lightmapTexture);
            var lightmapCopyPath = $"{folder}/{lightmapPrefix}.exr";
            AssetDatabase.CopyAsset(lightmapPath, lightmapCopyPath);

            //刷新
            AssetDatabase.Refresh();

            //修改Lightmap设置
            AssetDatabase.StartAssetEditing();
            var importer = AssetImporter.GetAtPath(lightmapCopyPath) as TextureImporter;
            if (importer != null)
            {
                importer.isReadable = true;
                importer.mipmapEnabled = false;

                var androidSetting = importer.GetPlatformTextureSettings("Android");
                androidSetting.overridden = true;
                androidSetting.format = TextureImporterFormat.ASTC_6x6;
                importer.SetPlatformTextureSettings(androidSetting);

                var iosSetting = importer.GetPlatformTextureSettings("iPhone");
                iosSetting.overridden = true;
                iosSetting.format = TextureImporterFormat.ASTC_6x6;
                importer.SetPlatformTextureSettings(iosSetting);

                var pcSetting = importer.GetPlatformTextureSettings("Standalone");
                pcSetting.overridden = true;
                pcSetting.format = TextureImporterFormat.BC6H;
                importer.SetPlatformTextureSettings(pcSetting);
                    
                importer.SaveAndReimport();
            }
            AssetDatabase.StopAssetEditing();
        }
        
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
    }   
}