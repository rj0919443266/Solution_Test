using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Xml.Serialization;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace WpfControlLibrary1
{
    public static class C_XML
    {
        // 建立 XmlSerializer 的快取池。
        // XmlSerializer 的建構子非常耗效能，透過快取可以大幅提升重複執行時的速度。
        private static readonly ConcurrentDictionary<Type, XmlSerializer> SerializerCache = new ConcurrentDictionary<Type, XmlSerializer>();

        /// <summary>
        /// 取得或建立 XmlSerializer (內部快取機制)
        /// </summary>
        private static XmlSerializer GetSerializer(Type type)
        {
            return SerializerCache.GetOrAdd(type, t => new XmlSerializer(t));
        }

        /// <summary>
        /// 將物件序列化成XML格式字串
        /// </summary>
        /// <typeparam name="T">物件型別</typeparam>
        /// <param name="obj">物件</param>
        /// <returns>XML格式字串</returns>
        public static string Serialize<T>(T obj)
        {
            if (obj == null) return string.Empty;

            try
            {
                XmlSerializer ser = GetSerializer(typeof(T));
                //使用 using 確保 StringWriter 資源正確釋放
                using (StringWriter writer = new StringWriter())
                {
                    ser.Serialize(writer, obj);
                    return writer.ToString();
                }
            }
            catch (Exception e)
            {
                Debug.Print($"序列化字串失敗: {e.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 將物件序列化並儲存成XML檔案
        /// </summary>
        /// <typeparam name="T">物件型別</typeparam>
        /// <param name="obj">物件</param>
        /// <param name="path">檔案儲存路徑</param>
        public static void Save_XML_from_object<T>(T obj, string path)
        {
            if (obj == null || string.IsNullOrWhiteSpace(path)) return;

            try
            {
                XmlSerializer ser = GetSerializer(typeof(T));
                // StreamWriter 預設會使用 UTF-8 寫入
                using (StreamWriter writer = new StreamWriter(path))
                {
                    ser.Serialize(writer, obj);
                }
            }
            catch (Exception e)
            {
                Debug.Print($"序列化至檔案失敗: {e.Message}");
            }
        }

        /// <summary>
        /// 將XML格式字串反序列化成物件
        /// </summary>
        /// <typeparam name="T">物件型別</typeparam>
        /// <param name="xmlString">XML格式字串</param>
        /// <returns>反序列化後的物件</returns>
        public static T Deserialize<T>(string xmlString)
        {
            if (string.IsNullOrWhiteSpace(xmlString)) return default(T);

            try
            {
                XmlSerializer ser = GetSerializer(typeof(T));
                //捨棄 XmlDocument，直接用 StringReader 效能最好
                using (StringReader reader = new StringReader(xmlString))
                {
                    return (T)ser.Deserialize(reader);
                }
            }
            catch (Exception e)
            {
                Debug.Print($"反序列化字串失敗: {e.Message}");
                return default(T);
            }
        }

        /// <summary>
        /// 從XML檔案反序列化成物件
        /// </summary>
        /// <typeparam name="T">物件型別</typeparam>
        /// <param name="path">檔案路徑</param>
        /// <returns>反序列化後的物件</returns>
        public static T Get_obj_From_XML<T>(string path) 
        {
            // 先檢查檔案是否存在，避免不必要的 Exception
            if (!File.Exists(path))
            {
                Debug.Print($"找不到指定的 XML 檔案: {path}");
                return default(T);
            }

            try
            {
                XmlSerializer ser = GetSerializer(typeof(T));
                using (StreamReader reader = new StreamReader(path))
                {
                    return (T)ser.Deserialize(reader);
                }
            }
            catch (Exception e)
            {
                Debug.Print($"從檔案反序列化失敗: {e.Message}");
                return default(T);
            }
        }
    }
}
