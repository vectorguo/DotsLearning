using System.Collections.Generic;
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
        private const int c_hqLightmapSize = 2048;
        private const int c_hqLightmapBakeResolution = 6;
        
        /// <summary>
        /// 中精度Lightmap大小
        /// </summary>
        private const int c_mqLightmapSize = 1024;
        private const int c_mqLightmapBakeResolution = 4;
        
        /// <summary>
        /// 低精度Lightmap大小
        /// </summary>
        private const int c_lqLightmapSize = 512;
        private const int c_lqLightmapBakeResolution = 2;
        
        public static void BakeLightmap(Dictionary<int, List<BigWorldBakerHelper.BigWorldBakeGroup>> bakeGroupsMap)
        {
            foreach (var pair in bakeGroupsMap)
            {
                var cellIndex = pair.Key;
                BigWorldBakerHelper.GetCellCoordinate(cellIndex, out var cellX, out var cellZ);
                
                //首先清理场景对象的Lightmap参数
                ClearLightmapParams();
                
                //给场景对象设置新的Lightmap参数
                var bakeTag = 100;
                foreach (var bakeGroup in pair.Value)
                {
                    var lightmapParam = new LightmapParameters()
                    {
                        bakedLightmapTag = bakeTag++,
                        limitLightmapCount = true,
                        maxLightmapCount = 1,
                    };
                    
                    foreach (var item in bakeGroup.items)
                    {
                        SetLightmapParamsOfRenderer(item.renderer, lightmapParam);
                    }
                }
                
                //烘焙高精度Lightmap
                ClearLightmap();
                BakeLightmap(c_hqLightmapSize, c_hqLightmapBakeResolution);
                
                //break;
            }
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
        private static void SetLightmapParamsOfRenderer(MeshRenderer renderer, LightmapParameters lightmapParam)
        {
            renderer.scaleInLightmap = 1.0f;
            renderer.stitchLightmapSeams = false;

            var so = new SerializedObject(renderer);
            var sp = so.FindProperty("m_LightmapParameters");
            sp.objectReferenceValue = lightmapParam;
            so.ApplyModifiedProperties();
                    
            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.ContributeGI);
        }
    }   
}