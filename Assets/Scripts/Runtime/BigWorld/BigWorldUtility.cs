using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace BigCat.BigWorld
{
    public static class BigWorldUtility
    {
        #region 坐标转换
        /// <summary>
        /// 格子大小
        /// </summary>
        public const int cellSize = 256;

        /// <summary>
        /// 每一行最多有多少个
        /// </summary>
        private const int c_cellRowCount = 1024;

        /// <summary>
        /// 大世界原点的格子偏移
        /// </summary>
        public const int bigWorldOriginCellOffset = 50;

        /// <summary>
        /// 大世界原点偏移
        /// </summary>
        public const float bigWorldOriginOffset = -1024 * bigWorldOriginCellOffset;

        /// <summary>
        /// 获取基于大世界原点的格子坐标
        /// </summary>
        /// <param name="worldPosition">世界坐标</param>
        /// <returns>基于大世界原点的格子坐标</returns>
        public static int GetCellCoordinateBaseOri(float worldPosition)
        {
            return (int)((worldPosition - bigWorldOriginOffset) / cellSize);
        }

        /// <summary>
        /// 获取基于Zero的格子坐标
        /// </summary>
        /// <param name="worldPosition">世界坐标</param>
        /// <returns>基于大Zero的格子坐标</returns>
        public static int GetCoordinateBaseZero(float worldPosition)
        {
            return GetCellCoordinateBaseOri(worldPosition) - (1024 / cellSize) * bigWorldOriginCellOffset;
        }

        /// <summary>
        /// 获取Cell的索引
        /// </summary>
        /// <param name="cellX">Cell的X轴坐标</param>
        /// <param name="cellZ">Cell的Z轴坐标</param>
        /// <returns></returns>
        public static int GetCellIndex(int cellX, int cellZ)
        {
            return cellZ * c_cellRowCount + cellX;
        }

        /// <summary>
        /// 将Cell所以转换成对应的坐标
        /// </summary>
        /// <param name="cellIndex">Cell索引</param>
        /// <param name="cellX">Cell的X轴坐标</param>
        /// <param name="cellZ">Cell的Z轴卓表</param>
        public static void GetCellCoordinate(int cellIndex, out int cellX, out int cellZ)
        {
            cellX = cellIndex % c_cellRowCount;
            cellZ = cellIndex / c_cellRowCount;
        }
        #endregion

        #region PackedMatrix
        public struct PackedMatrix
        {
            public float c0x; public float c0y; public float c0z;
            public float c1x; public float c1y; public float c1z;
            public float c2x; public float c2y; public float c2z;
            public float c3x; public float c3y; public float c3z;

            public PackedMatrix(Matrix4x4 m)
            {
                c0x = m.m00; c0y = m.m10; c0z = m.m20;
                c1x = m.m01; c1y = m.m11; c1z = m.m21;
                c2x = m.m02; c2y = m.m12; c2z = m.m22;
                c3x = m.m03; c3y = m.m13; c3z = m.m23;
            }
        }
        #endregion
        
        /// <summary>
        /// maxGraphicsBufferSize
        /// </summary>
        private const long c_maxGraphicsBufferSize = 32 * 1024;
        public static long maxGraphicsBufferSize => Math.Min(c_maxGraphicsBufferSize, SystemInfo.maxGraphicsBufferSize);

        /// <summary>
        /// Lightmap格式
        /// </summary>
#if UNITY_ANDROID
        public const TextureFormat lightmapTextureFormat = TextureFormat.ASTC_6x6;
#elif UNITY_IOS
        public const TextureFormat lightmapTextureFormat = TextureFormat.ASTC_6x6;
#else
        public const TextureFormat lightmapTextureFormat = TextureFormat.DXT5;
#endif

        /// <summary>
        /// 分配内存
        /// </summary>
        public static unsafe T* Malloc<T>(uint count) where T : unmanaged
        {
            return (T*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<T>() * count, UnsafeUtility.AlignOf<T>(), Allocator.TempJob);
        }
    }
}