using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BigCatEditor.BigWorld
{
    public static class BigWorldLightmapBaker
    {
        /// <summary>
        /// 高精度Lightmap大小
        /// </summary>
        private const int c_hqLightmapSize = 1024;
        private const int c_hqLightmapBakeResolution = 6;
        
        /// <summary>
        /// 中精度Lightmap大小
        /// </summary>
        private const int c_mqLightmapSize = 1024;
        private const int c_mqLightmapBakeResolution = 4;
        
        /// <summary>
        /// 低精度Lightmap大小
        /// </summary>
        private const int c_lqLightmapSize = 1024;
        private const int c_lqLightmapBakeResolution = 3;
        
        public static void BakeLightmap(Dictionary<int, List<BigWorldBakerHelper.BigWorldBakeGroup>> bakeGroupsMap)
        {
            foreach (var pair in bakeGroupsMap)
            {
                //高精度
                PreBakeLightmap(pair.Value, BigWorldBakerHelper.LightmapQuality.High);
                BakeLightmap(c_hqLightmapSize, c_hqLightmapBakeResolution);
                PostBakeLightmap(pair.Key, pair.Value, BigWorldBakerHelper.LightmapQuality.High);

                //中精度
                PreBakeLightmap(pair.Value, BigWorldBakerHelper.LightmapQuality.Med);
                BakeLightmap(c_mqLightmapSize, c_mqLightmapBakeResolution);
                PostBakeLightmap(pair.Key, pair.Value, BigWorldBakerHelper.LightmapQuality.Med);

                //低精度
                PreBakeLightmap(pair.Value, BigWorldBakerHelper.LightmapQuality.Low);
                BakeLightmap(c_lqLightmapSize, c_lqLightmapBakeResolution);
                PostBakeLightmap(pair.Key, pair.Value, BigWorldBakerHelper.LightmapQuality.Low);
            }

            //刷新
            AssetDatabase.Refresh();
        }

        private static void PreBakeLightmap(List<BigWorldBakerHelper.BigWorldBakeGroup> bakeGroups, BigWorldBakerHelper.LightmapQuality quality)
        {
            //首先清理场景对象的Lightmap参数
            ClearLightmap();
            ClearLightmapParams();

            //给场景对象设置新的Lightmap参数
            var lightmapParam = new LightmapParameters() { bakedLightmapTag = 100 };
            foreach (var bakeGroup in bakeGroups)
            {
                foreach (var item in bakeGroup.items)
                {
                    SetLightmapParamsOfRenderer(item.renderer, lightmapParam, quality, false);
                }
            }
        }

        private static void PostBakeLightmap(int cellIndex, List<BigWorldBakerHelper.BigWorldBakeGroup> bakeGroups, BigWorldBakerHelper.LightmapQuality quality)
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
            foreach (var bakeGroup in bakeGroups)
            {
                foreach (var item in bakeGroup.items)
                {
                    var lightmapIndex = item.renderer.lightmapIndex;
                    if (!lightmapIndices.Contains(lightmapIndex))
                    {
                        lightmapIndices.Add(lightmapIndex);
                    }

                    if (quality == BigWorldBakerHelper.LightmapQuality.High)
                    {
                        item.hqLightmapIndex = lightmapIndices.IndexOf(lightmapIndex);
                        item.hqLightmapScaleOffset = item.renderer.lightmapScaleOffset;
                    }
                    else if (quality == BigWorldBakerHelper.LightmapQuality.Med)
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
            var lightmapCopyPathes = new List<string>();
            var lightmapPrefix = quality == BigWorldBakerHelper.LightmapQuality.High ? "shq_lightmap" : (quality == BigWorldBakerHelper.LightmapQuality.Med ? "smq_lightmap" : "slq_lightmap");
            for (var i = 0; i < lightmapIndices.Count; ++i)
            {
                var lightmapIndex = lightmapIndices[i];
                var lightmapTexture = LightmapSettings.lightmaps[lightmapIndex].lightmapColor;
                var lightmapPath = AssetDatabase.GetAssetPath(lightmapTexture);
                var lightmapCopyPath = $"{folder}/{lightmapPrefix}_{i}.exr";
                lightmapCopyPathes.Add(lightmapCopyPath);
                AssetDatabase.CopyAsset(lightmapPath, lightmapCopyPath);
            }

            //刷新
            AssetDatabase.Refresh();

            //修改Lightmap设置
            AssetDatabase.StartAssetEditing();
            foreach (var path in lightmapCopyPathes)
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
        private static void SetLightmapParamsOfRenderer(MeshRenderer renderer, LightmapParameters lightmapParam, BigWorldBakerHelper.LightmapQuality quality, bool castShadowOnly)
        {
            if (castShadowOnly)
            {
                renderer.scaleInLightmap = 0;
            }
            else
            {
                if (quality == BigWorldBakerHelper.LightmapQuality.High)
                {
                    renderer.scaleInLightmap = 1.0f;
                }
                else if (quality == BigWorldBakerHelper.LightmapQuality.Med)
                {
                    renderer.scaleInLightmap = 0.75f;
                }
                else
                {
                    renderer.scaleInLightmap = 0.5f;
                }
            }
            renderer.stitchLightmapSeams = false;

            var so = new SerializedObject(renderer);
            var sp = so.FindProperty("m_LightmapParameters");
            sp.objectReferenceValue = lightmapParam;
            so.ApplyModifiedProperties();
                    
            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.ContributeGI);
        }
    }   
}