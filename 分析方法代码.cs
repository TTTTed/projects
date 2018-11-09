using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using DPCClassLibrary.MSSQLAccess;
using DPCClassLibrary.Base;

namespace AVC_Data_Analysis_Pro
{
    /// <summary>
    /// 在内存中进行机型的查找
    /// </summary>
   public class ModelSearch 
    {
        /// <summary>
        /// 商家型号对照表记录结构
        /// </summary>
        public struct BusinessModelInfo
        {
            /// <summary>
            /// 品类
            /// </summary>
            public string Category { get; set; }
            /// <summary>
            /// 品牌
            /// </summary>
            public string Brand { get; set; }
            /// <summary>
            /// 商家机型
            /// </summary>
            public string BusinessModel { get; set; }
            /// <summary>
            /// 机型
            /// </summary>
            public string RealModel { get; set; }
        }
        //保存从商家型号对照表中读取的对照数据
        //BusinessModelInfo[] ModelInfo;
        BusinessModelInfo resultModelInfo = new BusinessModelInfo();
        //索引表(保存0-9,a-z)的下标信息
        //Dictionary<string, int> indexTable = new Dictionary<string, int>();
        /// <summary>
        /// 短语到品类的映射
        /// </summary>
        Dictionary<string, string> PhaseToCategory = new Dictionary<string, string>();//有用品类的词汇表
        /// <summary>
        /// 无效中文词汇集合
        /// </summary>
        List<string> InvalidChinesePhaseList = new List<string>();
        /// <summary>
        /// 无效英文词汇集合
        /// </summary>
        List<string> InvalidEnglishPhaseList = new List<string>();
        /// <summary>
        /// 品牌及其别称、英文名
        /// </summary>
        Dictionary<string, List<string>> BrandAndItsAlias = new Dictionary<string, List<string>>();
        List<string> CClassifier = new List<string>() { "升", "立升", "寸", "英寸", "斤", "公斤" };//中文量词
        List<string> EClassifier = new List<string>() { "kg", "cm", "l", "p" };//英文量词
        List<string> specialNeedless = new List<string>() { "1.5P" };
        //string[] keys;
        string sqlcmd = "";
        bool Initialized = false;
        Dictionary<string, Dictionary<string, Dictionary<string, string>>> ModelInformation = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();
        static DataTable URLStore = null;
        string[] catsToLoad = null;

        public ModelSearch(string[] categoriesToLoad)
        {
            //初始化对照数据
            this.catsToLoad = categoriesToLoad;
            Initialized = BusinessModelInfoInitializing();
            Initialized = VocabularyInitializing();
            Initialized = URLStoreInitializing();
            BrandAndItsAlias = InitializingBrandDic()["全部"];
            if (!Initialized)
                throw new Exception("初始化失败，请重试！");
        }

        public ModelSearch()
        {
            //初始化对照数据
            this.catsToLoad = new string[] { };
            Initialized = BusinessModelInfoInitializing();
            Initialized = VocabularyInitializing();
            BrandAndItsAlias = InitializingBrandDic()["全部"];
            if (!Initialized)
                throw new Exception("初始化失败，请重试！");
        }

        /// <summary>
        /// 获取指定品类组包含的品牌及其别名所组成的字典
        /// </summary>
        /// <param name="categoryGroupTablePrefix">为各品类组设计的数据库表名前缀</param>
        /// <returns></returns>
        public static Dictionary<string, Dictionary<string, List<string>>> InitializingBrandDic()
        {
            Dictionary<string, Dictionary<string, List<string>>> result = new Dictionary<string, Dictionary<string, List<string>>>();
            MSSQLExecute mysql = new MSSQLExecute(MyConfiguration.Source);
            string sqlcmd = "SELECT 品类,A.品牌,品牌英文,品牌别名 FROM " +
                "(SELECT 品牌,品牌英文,品牌别名 FROM 品牌表 WHERE 品牌<>'其他') A," +
                "(SELECT 品牌,品类 FROM 型号表 GROUP BY 品牌,品类) B " +
                " WHERE A.品牌=B.品牌 "+
                "GROUP BY 品类,A.品牌,品牌英文,品牌别名 ORDER BY A.品牌 DESC,LEN(A.品牌) DESC,LEN(品牌别名) DESC,品牌别名 DESC";
            DataTable dataTable = mysql.ExecuteQuery(sqlcmd);
            if (dataTable == null)
                return null;
            if (dataTable.Rows.Count == 0)
                return null;
            Dictionary<string, List<string>> OtherCategory = new Dictionary<string, List<string>>();
            for (int i = 0; i < dataTable.Rows.Count; i++)
            {
                string category = dataTable.Rows[i]["品牌"].ToString();
                string brand = dataTable.Rows[i]["品牌"].ToString();
                string brandEnglishName = dataTable.Rows[i]["品牌英文"].ToString();
                string brandAliasSeries = dataTable.Rows[i]["品牌别名"].ToString();
                if (!OtherCategory.ContainsKey(brand))
                {
                    if (brandEnglishName.Length >= 2)
                        OtherCategory.Add(brand, new List<string>() { brandEnglishName });
                    else
                        OtherCategory.Add(brand, new List<string>());
                }
                string[] otherAlias = brandAliasSeries.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string a in otherAlias)
                    if (!OtherCategory[brand].Contains(a) && a.Length >= 2)
                        OtherCategory[brand].Add(a);
                if (result.ContainsKey(category))
                {
                    if (result[category].ContainsKey(brand))
                    {
                        if (!result[category][brand].Contains(brandEnglishName) && brandEnglishName.Length >= 2)
                            result[category][brand].Add(brandEnglishName);
                        string[] alias = brandAliasSeries.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string a in alias)
                            if (!result[category][brand].Contains(a) && a.Length >= 2)
                                result[category][brand].Add(a);
                    }
                    else
                    {
                        if (brandEnglishName.Length >= 2)
                            result[category].Add(brand, new List<string>() { brandEnglishName });
                        string[] alias = brandAliasSeries.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string a in alias)
                            if (!result[category][brand].Contains(a) && a.Length >= 2)
                                result[category][brand].Add(a);
                    }
                }
                else
                {
                    result.Add(category, new Dictionary<string, List<string>>());
                    if (brandEnglishName.Length >= 2)
                        result[category].Add(brand, new List<string>() { brandEnglishName });
                    else
                        result[category].Add(brand, new List<string>());
                    string[] alias = brandAliasSeries.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string a in alias)
                        if (!result[category][brand].Contains(a) && a.Length >= 2)
                            result[category][brand].Add(a);
                }
            }
            result.Add("全部", OtherCategory);
            return result;
        }
       /// <summary>
        /// 获取指定品类组包含的品牌及其别名所组成的字典+品类+其他
       /// </summary>
       /// <returns></returns>
       /// 
        public static Dictionary<string, Dictionary<string, List<string>>> InitializingBrandDicCategory()
        {
            Dictionary<string, Dictionary<string, List<string>>> result = new Dictionary<string, Dictionary<string, List<string>>>();
            MSSQLExecute mysql = new MSSQLExecute(MyConfiguration.Source);
            string sqlcmd = "SELECT 品类,A.品牌,品牌英文,品牌别名 FROM " +
                "(SELECT 品牌,品牌英文,品牌别名 FROM 品牌表 WHERE 品牌<>'其他') A," +
                "(SELECT 品牌,品类 FROM 型号表 GROUP BY 品牌,品类) B " +
                " WHERE A.品牌=B.品牌 " +
                "GROUP BY 品类,A.品牌,品牌英文,品牌别名 ORDER BY A.品牌 DESC,LEN(A.品牌) DESC,LEN(品牌别名) DESC,品牌别名 DESC";
            DataTable dataTable = mysql.ExecuteQuery(sqlcmd);
            if (dataTable == null)
                return null;
            if (dataTable.Rows.Count == 0)
                return null;
        
            //查询品类
            string[] qCategory = (from p in dataTable.AsEnumerable()
                             group p by p.Field<string>("品类") into g
                             select g.Key).ToArray();
            for (int i = 0; i < qCategory.Length; i++)
            {
                Dictionary<string, List<string>> OtherCategory = new Dictionary<string, List<string>>();

                var qBrand = (from p in dataTable.Select("品类='" + qCategory[i] + "'")
                              group p by new { brand = Convert.ToString(p.Field<object>("品牌")).ToUpper().Trim(), engBrandName = Convert.ToString(p.Field<object>("品牌英文")).ToUpper().Trim(), otherBrandName = Convert.ToString(p.Field<object>("品牌别名")).ToUpper().Trim() } into g
                              select g);
                foreach (var q in qBrand)
                {
                    if (!OtherCategory.ContainsKey(q.Key.brand))
                    {
                        if (q.Key.engBrandName.Length >= 2)
                            OtherCategory.Add(q.Key.brand, new List<string>() { q.Key.engBrandName });
                        else
                            OtherCategory.Add(q.Key.brand, new List<string>());
                    }
                }
                result.Add(qCategory[i], OtherCategory);
            }
            Dictionary<string, List<string>> OtherCategory2 = new Dictionary<string, List<string>>();
            for (int i = 0; i < dataTable.Rows.Count; i++)
            {
                string category = dataTable.Rows[i]["品牌"].ToString();
                string brand = dataTable.Rows[i]["品牌"].ToString();
                string brandEnglishName = dataTable.Rows[i]["品牌英文"].ToString();
                string brandAliasSeries = dataTable.Rows[i]["品牌别名"].ToString();
                if (!OtherCategory2.ContainsKey(brand))
                {
                    if (brandEnglishName.Length >= 2)
                        OtherCategory2.Add(brand, new List<string>() { brandEnglishName });
                    else
                        OtherCategory2.Add(brand, new List<string>());
                }
                string[] otherAlias = brandAliasSeries.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string a in otherAlias)
                    if (!OtherCategory2[brand].Contains(a) && a.Length >= 2)
                        OtherCategory2[brand].Add(a);
                if (result.ContainsKey(category))
                {
                    if (result[category].ContainsKey(brand))
                    {
                        if (!result[category][brand].Contains(brandEnglishName) && brandEnglishName.Length >= 2)
                            result[category][brand].Add(brandEnglishName);
                        string[] alias = brandAliasSeries.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string a in alias)
                            if (!result[category][brand].Contains(a) && a.Length >= 2)
                                result[category][brand].Add(a);
                    }
                    else
                    {
                        if (brandEnglishName.Length >= 2)
                            result[category].Add(brand, new List<string>() { brandEnglishName });
                        string[] alias = brandAliasSeries.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string a in alias)
                            if (!result[category][brand].Contains(a) && a.Length >= 2)
                                result[category][brand].Add(a);
                    }
                }
                else
                {
                    result.Add(category, new Dictionary<string, List<string>>());
                    if (brandEnglishName.Length >= 2)
                        result[category].Add(brand, new List<string>() { brandEnglishName });
                    else
                        result[category].Add(brand, new List<string>());
                    string[] alias = brandAliasSeries.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string a in alias)
                        if (!result[category][brand].Contains(a) && a.Length >= 2)
                            result[category][brand].Add(a);
                }
            }
            result.Add("全部", OtherCategory2);
            return result;
        }

        //初始化对照数据,初始化索引表
        private bool BusinessModelInfoInitializing()
        {
            int resultCount = 0;
            string currentIndexStr = string.Empty, lastIndexStr = string.Empty;//当前索引字符和上一个索引字符
            sqlcmd = "SELECT LOWER(商家机型) 商家机型,LOWER(机型) 机型,品类,LOWER(品牌) 品牌 FROM 商家型号对照表";// ORDER BY LOWER(商家机型) COLLATE Chinese_PRC_BIN";
            MSSQLExecute sqlexe = new MSSQLExecute(MyConfiguration.Source);
            //初始化数据
            DataTable dataTable = sqlexe.ExecuteQuery(sqlcmd);
            if (dataTable == null)
                return false;
            if (dataTable.Rows.Count == 0)
                return false;
            resultCount = dataTable.Rows.Count;
            for (int i = 0; i < resultCount; i++)
            {
                if (ModelInformation.ContainsKey(dataTable.Rows[i]["品类"].ToString()))
                {
                    if (ModelInformation[dataTable.Rows[i]["品类"].ToString()].ContainsKey(dataTable.Rows[i]["品牌"].ToString()))
                    {
                        if (ModelInformation[dataTable.Rows[i]["品类"].ToString()][dataTable.Rows[i]["品牌"].ToString()].ContainsKey(dataTable.Rows[i]["商家机型"].ToString()))
                        {
                            ModelInformation[dataTable.Rows[i]["品类"].ToString()][dataTable.Rows[i]["品牌"].ToString()][dataTable.Rows[i]["商家机型"].ToString()] = dataTable.Rows[i]["机型"].ToString();
                        }
                        else
                        {
                            ModelInformation[dataTable.Rows[i]["品类"].ToString()][dataTable.Rows[i]["品牌"].ToString()].Add(dataTable.Rows[i]["商家机型"].ToString(), dataTable.Rows[i]["机型"].ToString());
                        }
                    }
                    else
                    {
                        ModelInformation[dataTable.Rows[i]["品类"].ToString()].Add(dataTable.Rows[i]["品牌"].ToString(), new Dictionary<string, string>() { { dataTable.Rows[i]["商家机型"].ToString(), dataTable.Rows[i]["机型"].ToString() } });
                    }
                }
                else
                {
                    ModelInformation.Add(dataTable.Rows[i]["品类"].ToString(), new Dictionary<string, Dictionary<string, string>>() { { dataTable.Rows[i]["品牌"].ToString(), new Dictionary<string, string>() { { dataTable.Rows[i]["商家机型"].ToString(), dataTable.Rows[i]["机型"].ToString() } } } });
                }
            }
            #region
            //ModelInfo = new BusinessModelInfo[resultCount];
            ////设置第一个索引
            ////currentIndexStr = dataTable.Rows[0][0].ToString().Substring(0, 1).ToLower();
            //currentIndexStr = StringOperation.StringCleanUp(dataTable.Rows[0][0].ToString().Trim()).Substring(0, 1).ToLower();
            //lastIndexStr = currentIndexStr;
            //tempstr = currentIndexStr;
            //indexTable.Add(currentIndexStr, 0);
            //for (int i = 0; i < resultCount; i++)
            //{
            //    ModelInfo[i].BusinessModel = dataTable.Rows[i][0].ToString();
            //    ModelInfo[i].RealModel = dataTable.Rows[i][1].ToString();
            //    ModelInfo[i].Category = dataTable.Rows[i][2].ToString();
            //    ModelInfo[i].Brand = dataTable.Rows[i][3].ToString();
            //    //判断是否需要增加新的索引
            //    //currentIndexStr = dataTable.Rows[i][0].ToString().Substring(0, 1).ToLower();
            //    currentIndexStr = StringOperation.StringCleanUp(dataTable.Rows[i][0].ToString().Trim()).Substring(0, 1).ToLower();
            //    if (currentIndexStr != lastIndexStr)
            //    {
            //        if (tempstr.Contains(currentIndexStr))
            //            continue;
            //        else
            //        {
            //            indexTable.Add(currentIndexStr, i);
            //            lastIndexStr = currentIndexStr;
            //            tempstr += currentIndexStr;
            //        }
            //    }
            //}
            ////添加最后的长度索引，Z之后
            //indexTable.Add("end", resultCount);
            #endregion
            return true;
        }

        //初始化词库表
        private bool VocabularyInitializing()
        {
            MSSQLExecute sqlexe = new MSSQLExecute(MyConfiguration.Source);
            DataTable dataTable = new DataTable();
            string sqlcmd = string.Empty;
            //初始化有用词汇
            sqlcmd = "SELECT 原词,类别 FROM 零售词汇表 WHERE 是否需要=1 AND LEN(原词)>1 ORDER BY LEN(原词) DESC";
            dataTable = sqlexe.ExecuteQuery(sqlcmd);
            if (dataTable == null)
                return false;
            if (dataTable.Rows.Count == 0)
                return false;
            for (int i = 0; i < dataTable.Rows.Count; i++)
                PhaseToCategory.Add(dataTable.Rows[i]["原词"].ToString().Trim(), dataTable.Rows[i]["类别"].ToString().Trim());
            //初始化中文无用词汇表
            sqlcmd = "SELECT 原词 FROM 零售词汇表 WHERE 是否需要=0 AND LEN(原词)>1 AND 原词 collate Chinese_PRC_CI_AS LIKE '%[啊-座]%' ORDER BY LEN(原词) DESC";
            dataTable = sqlexe.ExecuteQuery(sqlcmd);
            if (dataTable == null)
                return false;
            if (dataTable.Rows.Count == 0)
                return false;
            for (int i = 0; i < dataTable.Rows.Count; i++)
                InvalidChinesePhaseList.Add(dataTable.Rows[i]["原词"].ToString().Trim());
            //初始化英文无用词汇表
            sqlcmd = "SELECT 原词 FROM 零售词汇表 WHERE 是否需要=0 AND LEN(原词)>1 AND 原词 collate Chinese_PRC_CI_AS LIKE '%[a-z]%' ORDER BY LEN(原词) DESC";
            dataTable = sqlexe.ExecuteQuery(sqlcmd);
            if (dataTable == null)
                return false;
            if (dataTable.Rows.Count == 0)
                return false;
            for (int i = 0; i < dataTable.Rows.Count; i++)
                InvalidEnglishPhaseList.Add(dataTable.Rows[i]["原词"].ToString().Trim());
            return true;
        }

        private bool URLStoreInitializing()
        {
            if (URLStore == null && catsToLoad.Length > 0)
            {
                string filter = " AND urlleibie in (";
                for (int i = 0; i < catsToLoad.Length; i++)
                {
                    if (catsToLoad[i] != null && catsToLoad[i].Trim().Length > 0)
                        filter += "'" + catsToLoad[i] + "',";
                }
                filter = filter.Remove(filter.Length - 1) + ")";
                MSSQLExecute mysql = new MSSQLExecute(MyConfiguration.CrawlerSource);
                string cmd = "SELECT DISTINCT URLLEIBIE 品类,SPNAME 商品名称 FROM URLDATA_ALL WHERE NEED = 1 AND URLS LIKE 'HTTP://%'" + filter;
                URLStore = mysql.ExecuteQuery(cmd);
            }
            return true;
        }

        ////折半查找算法
        //private BusinessModelInfo BiSearch(BusinessModelInfo Model, int begin, int last)
        //{
        //    if (begin > last)
        //    {
        //        BusinessModelInfo result = new BusinessModelInfo();
        //        result.RealModel = string.Empty;
        //        return result;
        //    }
        //    int mid = (begin + last) / 2;
        //    if (Model.Category.Length != 0)
        //    {
        //        //如果机型和对照表中的机型，品牌，品类一致返回该记录
        //        if (Model.BusinessModel.ToLower() == ModelInfo[mid].BusinessModel.ToLower() &&
        //        Model.Brand.ToLower() == ModelInfo[mid].Brand.ToLower() &&
        //        Model.Category.ToLower() == ModelInfo[mid].Category.ToLower())
        //            return ModelInfo[mid];
        //        else
        //        {
        //            //如果商家机型相等，尝试查找上下界的商家机型--对应商家机型一样记录
        //            if (Model.BusinessModel.ToLower() == ModelInfo[mid].BusinessModel.ToLower())
        //            {
        //                //往下界查找相同商家机型的记录
        //                for (int i = mid;i >= begin;i--)
        //                {
        //                    if (Model.BusinessModel.ToLower() == ModelInfo[i].BusinessModel.ToLower())
        //                    {
        //                        if (Model.BusinessModel.ToLower() == ModelInfo[i].BusinessModel.ToLower() && Model.Brand.ToLower() == ModelInfo[i].Brand.ToLower() && Model.Category.ToLower() == ModelInfo[mid].Category.ToLower())
        //                            return ModelInfo[i];
        //                    }
        //                    else
        //                        break;
        //                }
        //                //往上界查找相同商家机型的记录
        //                for (int i = mid;i <= last;i++)
        //                {
        //                    if (Model.BusinessModel.ToLower() == ModelInfo[i].BusinessModel.ToLower())
        //                    {
        //                        if (Model.BusinessModel.ToLower() == ModelInfo[i].BusinessModel.ToLower() && Model.Brand.ToLower() == ModelInfo[i].Brand.ToLower() && Model.Category.ToLower() == ModelInfo[mid].Category.ToLower())
        //                            return ModelInfo[i];
        //                    }
        //                    else
        //                        break;
        //                }
        //                //都没有找到时返回空
        //                BusinessModelInfo result = new BusinessModelInfo();
        //                result.RealModel = string.Empty;
        //                return result;
        //            }
        //        }
        //    }
        //    else
        //    {
        //        //如果机型和对照表中的机型，品牌一致返回该记录
        //        if (Model.BusinessModel.ToLower() == ModelInfo[mid].BusinessModel.ToLower() && Model.Brand.ToLower() == ModelInfo[mid].Brand.ToLower())
        //            return ModelInfo[mid];
        //        else
        //        {
        //            //如果商家机型相等，尝试查找上下界的商家机型--对应商家机型一样记录
        //            if (Model.BusinessModel.ToLower() == ModelInfo[mid].BusinessModel.ToLower())
        //            {
        //                //往下界查找相同商家机型的记录
        //                for (int i = mid;i >= begin;i--)
        //                {
        //                    if (Model.BusinessModel.ToLower() == ModelInfo[i].BusinessModel.ToLower())
        //                    {
        //                        if (Model.BusinessModel.ToLower() == ModelInfo[i].BusinessModel.ToLower() && Model.Brand.ToLower() == ModelInfo[i].Brand.ToLower())
        //                            return ModelInfo[i];
        //                    }
        //                    else
        //                        break;
        //                }
        //                //往上界查找相同商家机型的记录
        //                for (int i = mid;i <= last;i++)
        //                {
        //                    if (Model.BusinessModel.ToLower() == ModelInfo[i].BusinessModel.ToLower())
        //                    {
        //                        if (Model.BusinessModel.ToLower() == ModelInfo[i].BusinessModel.ToLower() && Model.Brand.ToLower() == ModelInfo[i].Brand.ToLower())
        //                            return ModelInfo[i];
        //                    }
        //                    else
        //                        break;
        //                }
        //                //都没有找到时返回空
        //                BusinessModelInfo result = new BusinessModelInfo();
        //                result.RealModel = string.Empty;
        //                return result;
        //            }
        //        }
        //    }
        //    //待查找的机型小于对照机型时mid-1递归调用
        //    int compareResult = CompareStringByASCII(Model.BusinessModel.ToLower(), ModelInfo[mid].BusinessModel.ToLower());
        //    if (compareResult < 0)
        //        return BiSearch(Model, begin, mid - 1);
        //    else if (compareResult > 0)
        //        return BiSearch(Model, mid + 1, last);
        //    else
        //        return ModelInfo[mid];

        //    //if (Model.BusinessModel.ToLower().CompareTo(ModelInfo[mid].BusinessModel.ToLower()) < 0)
        //    // return BiSearch(Model, begin, mid - 1);
        //    //else
        //    // return BiSearch(Model, mid + 1, last);
        //}

        ////机型查找
        //private BusinessModelInfo ModelQuery(string businessModel, string brand, string category)
        //{
        //    //型号,品牌为空时返回空字符串
        //    if (businessModel.Length == 0 || brand.Length == 0)
        //    {
        //        BusinessModelInfo result = new BusinessModelInfo();
        //        result.RealModel = string.Empty;
        //        return result;
        //    }
        //    //如果索引表初始化不成功返回空字符串
        //    if (indexTable.Keys.Count == 0)
        //    {
        //        BusinessModelInfo result = new BusinessModelInfo();
        //        result.RealModel = string.Empty;
        //        return result;
        //    }

        //    int begin = 0, last = 0;
        //    string downBoundIndex = string.Empty;
        //    string firstStr = string.Empty;
        //    keys = indexTable.Keys.ToArray();//把索引表KEY存入数组以便取得上下边界值
        //    //组成BusinessModelInfo结构
        //    BusinessModelInfo BusinessModelInfo = new BusinessModelInfo();
        //    BusinessModelInfo.BusinessModel = businessModel.ToLower();
        //    BusinessModelInfo.Brand = brand.ToLower();
        //    BusinessModelInfo.Category = category.ToLower();
        //    //获得需查找机型的在索引表中上下边界值
        //    try
        //    {
        //        firstStr = businessModel.ToLower().Substring(0, 1);
        //        if (indexTable.Keys.Contains(firstStr))
        //        {
        //            begin = indexTable[firstStr];
        //            //查找下界值 
        //            for (int i = 0;i < keys.Length;i++)
        //                if (keys[i] == firstStr)
        //                {
        //                    downBoundIndex = keys[i + 1];
        //                    break;
        //                }
        //            last = indexTable[downBoundIndex];
        //        }
        //    }
        //    catch (ArgumentOutOfRangeException e)
        //    {
        //        throw e;
        //    }
        //    catch (ArgumentNullException e)
        //    {
        //        throw e;
        //    }
        //    catch (Exception e)
        //    {
        //        throw e;
        //    }
        //    //查找机型
        //    return BiSearch(BusinessModelInfo, begin, last);
        //}

        private BusinessModelInfo ModelQuery(string businessModel, string brand, string category)
        {
            BusinessModelInfo info = new BusinessModelInfo();
            info.Category = category;
            info.Brand = brand;
            info.BusinessModel = businessModel.ToUpper();
            info.RealModel = "";
            if (category != "")
            {
                if (ModelInformation.ContainsKey(category))
                    if (ModelInformation[category].ContainsKey(brand.ToLower()))
                        if (ModelInformation[category][brand.ToLower()].ContainsKey(businessModel.ToLower()))
                            info.RealModel = ModelInformation[category][brand.ToLower()][businessModel.ToLower()].ToUpper();
            }
            //if (info.RealModel == "")
            //    foreach (KeyValuePair<string, Dictionary<string, Dictionary<string, string>>> pair in ModelInformation)
            //        foreach (KeyValuePair<string, Dictionary<string, string>> value in pair.Value)
            //            if (value.Key == brand.ToLower())
            //                if (value.Value.ContainsKey(businessModel.ToLower()))
            //                {
            //                    info.Category = pair.Key;
            //                    info.RealModel = ModelInformation[pair.Key][brand.ToLower()][businessModel.ToLower()].ToUpper();
            //                }
            return info;
        }

        //字符串以ASCII值大小排序-1:x<y,0:x=y,1:x>y
        private int CompareStringByASCII(string x, string y)
        {
            if (x == null)
            {
                if (y == null)
                    // If x is null AND y is null, they're equal. 
                    return 0;
                else
                    // If x is null AND y is not null, y is greater. 
                    return 1;
            }
            else
            {
                // If x is not null
                if (y == null)
                    // ...and y is null, x is greater.
                    return -1;
                else
                {
                    // ...and y is not null, compare the lengths of the two strings.
                    int retval = -1;
                    int result = 0;//默认相等
                    ASCIIEncoding asciiEncoding = new ASCIIEncoding();
                    byte[] a = asciiEncoding.GetBytes(x);
                    byte[] b = asciiEncoding.GetBytes(y);
                    if (x.Length == y.Length)
                        retval = 0;
                    else if (x.Length > y.Length)
                        retval = 1;
                    else
                        retval = -1;
                    switch (retval)
                    {
                        case 0:
                            //X,Y长度相同
                            for (int i = 0; i < a.Length; i++)
                            {
                                if (a[i] > b[i])
                                {
                                    result = 1; break;
                                }
                                if (a[i] < b[i])
                                {
                                    result = -1; break;
                                }
                            }
                            break;
                        case 1:
                            // X的长度大于Y
                            for (int i = 0; i < b.Length; i++)
                            {
                                if (a[i] > b[i])
                                {
                                    result = 1; break;
                                }
                                if (a[i] < b[i])
                                {
                                    result = -1; break;
                                }
                            }
                            //循环做完时X=Y，但X的长度大于Y，返回1
                            if (result == 0)
                                result = 1;
                            break;
                        default:
                            // X的小度大于Y
                            for (int i = 0; i < a.Length; i++)
                            {
                                if (a[i] > b[i])
                                {
                                    result = 1; break;
                                }
                                if (a[i] < b[i])
                                {
                                    result = -1; break;
                                }
                            }
                            //循环做完时X=Y，但X的小度大于Y，返回-1
                            if (result == 0)
                                result = -1;
                            break;
                    }
                    return result;
                }
            }
        }

        static StringBuilder sb = new StringBuilder("");


//--------------------------入口-----------------------------------
        /// <summary>
        /// 分析字符串中的品牌、品类、机型，返回-1分析失败;0分析成功包含需要的信息;1分析成功但不包含需要的信息
        /// </summary>
        /// <param name="originalString">原始字符串</param>
        /// <param name="brand">分析出的品牌</param>
        /// <param name="category">分析出的品类</param>
        /// <param name="Model">分析出的型号</param>
        /// <returns></returns>
        public int TransactionAnalysis(string originalString, ref string brand, ref string category, ref string Model)
        {
            //对原始字符串进行一次清洗
            string tempString = StringOperation.StringCleanUp(originalString).ToUpper();
            //原始字符串清洗后长度为0，返回分析失败
            if (tempString.Length == 0)
                return -1;
            //机型正则表达式            
            string ModelExpression = @"([a-zA-Z0-9][^\u4e00-\u9fa5,]+[a-zA-Z0-9|)])+";
            //中文信息正则表达式
            //string chineseExpression = @"([\u4e00-\u9fa5]{2,})+";
            //全英文信息
            //string engExpression = @"^[a-zA-Z]+$";
            //存放中文信息结果
            List<string> chieseInfo = new List<string>();
            bool categoryFound = false;
            //当没有从文件名或表名获得品类信息时进行品类分析
            if (category == null || category == "")
            {
                //if (QueryCategoryFromURLStore(tempString, ref category) == 0)
                //{
                //    categoryFound = true;
                //    Console.WriteLine("上个品类的匹配来自URL库");
                //}
                //else
                switch (TransactionAnalysis(tempString, ref category))
                {
                    case 1: return 1;
                    case 0: categoryFound = true;
                        Console.WriteLine("上个品类的匹配来自正则表达式"); break;
                    default: break;
                }
                //object[] ret = TransactionAnalysis(tempString, category);
                //switch (ret[0].ToString())
                //{
                //    case "1": return 1;
                //    case "0":
                //        category = ret[1].ToString();
                //        categoryFound = true;
                //        break;
                //    default: break;
                //}
            }
            else
                categoryFound = true;

            List<string> ModelInfo = new List<string>();//存放型号信息     
            //通过正则表达式截取原始字符串中的商家机型
            foreach (Match match in Regex.Matches(tempString, ModelExpression))
                if (!ModelInfo.Contains(match.Value))
                    ModelInfo.Add(match.Value);

            //判断字符串中应成对出现的符号，如（）只出现单个符号时分隔字符串
            //暂时只考虑字符串中只出现一个(的情况，其他情况待扩展功能
            for (int i = 0; i < ModelInfo.Count; i++)
                ModelInfo[i] = StringOperation.StringFilter(ModelInfo[i], tempString);//过滤通过正则表达式取出的型号中信息

            //去除字符长度小于3的型号信息（由于机顶盒很多机型长度为2，故取消此项约束，14年9月16日zgl）
            //for (int i = 0;i < ModelInfo.Count;i++)
            //    if (ModelInfo[i].Length < 3)
            //        ModelInfo.RemoveAt(i--);

            //去除型号信息中全英文的字符记录（目前已出现全英文或全数字的机型，故取消此项约束，14年9月16日zgl）
            //for (int i = 0;i < ModelInfo.Count;i++)
            //    if (Regex.IsMatch(ModelInfo[i], engExpression))
            //        ModelInfo.RemoveAt(i--);

            //去除商家机型中的特殊字符串，例如1.5L，1.5P等
            for (int i = 0; i < ModelInfo.Count; i++)
                if (specialNeedless.Contains(ModelInfo[i]))
                    ModelInfo.RemoveAt(i--);

            //得不到型号信息时跳过此条记录的分析（目前要求找不到机型的商品名称分析其是否是净水配件，故取消此项约束，14年12月19日zgl）
            //if (ModelInfo.Count < 1)
            //    return 1;

            List<string> FoundBrand = new List<string>();//存放品牌信息       
            //当没有从文件名或表名获得品牌信息时进行品牌分析
            if (brand.Length == 0)
            {
                if (categoryFound)
                    tempString = tempString.Replace(category, "");

                //取出品牌信息
                foreach (KeyValuePair<string, List<string>> pair in BrandAndItsAlias)
                {
                    if (tempString.Contains(pair.Key.ToUpper()))
                        if (!FoundBrand.Contains(pair.Key.ToUpper()))
                            FoundBrand.Add(pair.Key.ToUpper());
                    foreach (string value in pair.Value)
                        if (tempString.Contains(value.ToUpper()))
                            if (!FoundBrand.Contains(pair.Key.ToUpper()))
                                FoundBrand.Add(pair.Key.ToUpper());
                }

                //在型号信息中替换掉品牌信息
                for (int i = 0; i < ModelInfo.Count; i++)
                    for (int j = 0; j < FoundBrand.Count; j++)
                        ModelInfo[i] = ModelInfo[i].Replace(FoundBrand[j] + "_", "");
            }
            else
                FoundBrand.Add(brand);

            //通过品牌品类型号查询数据库
            string tempBrand = string.Empty, tempModel = string.Empty, tempCategory = string.Empty;
            List<string> ModelQueryRes = new List<string>();//保存查询得到的标准机型
            int resultCount = 0;
            string ModelWithoutBrandeng = string.Empty;//没有英文品牌开头的型号字符串
            if (categoryFound)
            {
                //找不到品牌信息有型号信息时返回原始字符串，有可能是新品牌
                if (FoundBrand.Count < 1 && ModelInfo.Count > 0)
                    return -1;
                //找不到品牌信息且找不到型号信息，则跳过分析
                if (FoundBrand.Count < 1 && ModelInfo.Count < 1)
                    return 1;
                foreach (string brandstr in FoundBrand)
                    foreach (string ModelStr in ModelInfo)
                    {
                        ModelWithoutBrandeng = ModelStr;
                        //如果机型以品牌的英文开头，去除品牌英文
                        if (BrandAndItsAlias.ContainsKey(brandstr))
                        {
                            foreach (string alias in BrandAndItsAlias[brandstr])
                                if (ModelStr.ToUpper().StartsWith(alias))
                                    ModelWithoutBrandeng = ModelWithoutBrandeng.Remove(0, alias.Length).Trim();
                        }
                        ModelWithoutBrandeng = Regex.Match(ModelWithoutBrandeng, ModelExpression).ToString();

                        //尝试查询由当前品类、品牌、商家机型确定的机型信息
                        resultModelInfo = ModelQuery(ModelWithoutBrandeng, brandstr, category);

                        if (resultModelInfo.RealModel.Length != 0)
                        {
                            category = resultModelInfo.Category;
                            tempBrand = brandstr;
                            tempModel = resultModelInfo.RealModel;
                            if (!ModelQueryRes.Contains(tempModel))
                                ModelQueryRes.Add(tempModel);
                            ++resultCount;
                        }
                        else if (ModelWithoutBrandeng.ToUpper().StartsWith("LED-") || ModelWithoutBrandeng.ToUpper().StartsWith("LCD-"))
                        {
                            #region 如果机型以LED-、LCD-开始尝试去除LED-、LCD-后查找数据库（有文件形式：32寸LED-型号）
                            resultModelInfo = ModelQuery(ModelWithoutBrandeng.Remove(0, 4), brandstr, category);
                            if (resultModelInfo.RealModel.Length != 0)
                            {
                                category = resultModelInfo.Category;
                                tempBrand = brandstr;
                                tempModel = resultModelInfo.RealModel;
                                if (!ModelQueryRes.Contains(tempModel))
                                    ModelQueryRes.Add(tempModel);
                                ++resultCount;
                            }
                            #endregion
                        }
                        else if (ModelWithoutBrandeng.ToUpper().Contains("P") && ModelWithoutBrandeng.ToUpper().IndexOf("P") < 4)
                        {
                            #region 尝试去除型号中的1.5P，3P，2P(有文件家来福写成：1.5P型号)
                            int pIndex = ModelWithoutBrandeng.ToUpper().IndexOf("P") + 1;
                            resultModelInfo = ModelQuery(ModelWithoutBrandeng.Remove(0, pIndex), brandstr, category);
                            if (resultModelInfo.RealModel.Length != 0)
                            {
                                category = resultModelInfo.Category;
                                tempBrand = brandstr;
                                tempModel = resultModelInfo.RealModel;
                                if (!ModelQueryRes.Contains(tempModel))
                                    ModelQueryRes.Add(tempModel);
                                ++resultCount;
                            }
                            #endregion
                        }
                        else if (ModelWithoutBrandeng.Contains(" "))
                        {
                            #region 源字符串中包含**匹（如1.5匹）子串，而**（1.5）被截入ModelWithoutBrandeng，尝试替换后进行查找
                            string[] temp = ModelWithoutBrandeng.Split(' ');
                            float tran;
                            if (float.TryParse(temp[temp.Length - 1], out tran))
                            {
                                resultModelInfo = ModelQuery(ModelWithoutBrandeng.Replace(temp[temp.Length - 1] + " ", ""), brandstr, category);
                                if (resultModelInfo.RealModel.Length != 0)
                                {
                                    category = resultModelInfo.Category;
                                    tempBrand = brandstr;
                                    tempModel = resultModelInfo.RealModel;
                                    if (!ModelQueryRes.Contains(tempModel))
                                        ModelQueryRes.Add(tempModel);
                                    ++resultCount;
                                }
                            }
                            #endregion
                        }

                    }

                if (ModelQueryRes.Count == 1)
                {
                    brand = tempBrand;
                    Model = ModelQueryRes[0];
                    return 0;
                }
                else
                {
                    if (FoundBrand.Count == 1 && (category == "净水器" || category == "饮水机") && (tempString.Contains("配件") || tempString.Contains("滤芯") || tempString.Contains("水龙头")))
                    {
                        brand = FoundBrand[0];
                        Model = "净水配件";
                        if (brand == "小米")
                        {
                            Model = "";
                            return -1;
                        }
                        else
                            return 0;
                    }
                }
                return -1;
            }

            //如果未找到品类
            foreach (string brandstr in FoundBrand)
                foreach (string ModelStr in ModelInfo)
                {
                    ModelWithoutBrandeng = ModelStr;
                    resultModelInfo = ModelQuery(ModelWithoutBrandeng, brandstr, string.Empty);
                    if (resultModelInfo.RealModel.Length != 0)
                    {
                        category = resultModelInfo.Category;
                        tempBrand = brandstr;
                        tempModel = resultModelInfo.RealModel;
                        tempCategory = resultModelInfo.Category;
                        if (!ModelQueryRes.Contains(tempModel))
                            ModelQueryRes.Add(tempModel);
                        ++resultCount;
                    }
                    else if (ModelWithoutBrandeng.ToUpper().StartsWith("LED-") || ModelWithoutBrandeng.ToUpper().StartsWith("LCD-"))
                    {
                        #region 如果机型以LED-开始尝试去除LED-后查找数据库（有文件形式：32寸LED-型号）
                        resultModelInfo = ModelQuery(ModelWithoutBrandeng.Remove(0, 4), brandstr, string.Empty);
                        if (resultModelInfo.RealModel.Length != 0)
                        {
                            category = resultModelInfo.Category;
                            tempBrand = brandstr;
                            tempModel = resultModelInfo.RealModel;
                            tempCategory = resultModelInfo.Category;
                            if (!ModelQueryRes.Contains(tempModel))
                                ModelQueryRes.Add(tempModel);
                            ++resultCount;
                        }
                        #endregion
                    }
                    else if (ModelWithoutBrandeng.ToUpper().Contains("P") && ModelWithoutBrandeng.ToUpper().IndexOf("P") < 4)
                    {
                        #region 尝试去除型号中的1.5P,3P,2P(有文件家来福写成：1.5P型号)
                        int pIndex = ModelWithoutBrandeng.ToUpper().IndexOf("P") + 1;
                        resultModelInfo = ModelQuery(ModelWithoutBrandeng.Remove(0, pIndex), brandstr, string.Empty);
                        if (resultModelInfo.RealModel.Length != 0)
                        {
                            category = resultModelInfo.Category;
                            tempBrand = brandstr;
                            tempModel = resultModelInfo.RealModel;
                            tempCategory = resultModelInfo.Category;
                            if (!ModelQueryRes.Contains(tempModel))
                                ModelQueryRes.Add(tempModel);
                            ++resultCount;
                        }
                        #endregion
                    }
                    else if (ModelWithoutBrandeng.Contains(" "))
                    {
                        #region 源字符串中包含**匹（如1.5匹）子串，而**（1.5）被截入ModelWithoutBrandeng，尝试替换后进行查找
                        string[] temp = ModelWithoutBrandeng.Split(' ');
                        float tran;
                        if (float.TryParse(temp[temp.Length - 1], out tran))
                        {
                            resultModelInfo = ModelQuery(ModelWithoutBrandeng.Replace(temp[temp.Length - 1] + " ", ""), brandstr, category);
                            if (resultModelInfo.RealModel.Length != 0)
                            {
                                category = resultModelInfo.Category;
                                tempBrand = brandstr;
                                tempModel = resultModelInfo.RealModel;
                                if (!ModelQueryRes.Contains(tempModel))
                                    ModelQueryRes.Add(tempModel);
                                ++resultCount;
                            }
                        }
                        #endregion
                    }
                }

            if (ModelQueryRes.Count == 1)
            {
                brand = tempBrand;
                Model = ModelQueryRes[0];
                category = tempCategory;
                return 0;
            }
            return -1;
        }

        /// 分析字符串中的品牌、品类，返回-1分析失败;0分析成功;1分析成功但不包含需要的信息
        /// </summary>
        /// <param name="originalString">原始字符串</param>
        /// <param name="brand">分析出的品牌</param>
        /// <param name="category">分析出的品类</param>
        /// <returns></returns>
        public int TransactionAnalysis(string originalString, ref string brand, ref string category)
        {
            //对原始字符串进行一次清洗
            string tempString = StringOperation.StringCleanUp(originalString).ToUpper();

            //原始字符串清洗后长度为0，返回分析失败
            if (tempString.Length == 0)
                return -1;

            //品类信息在外部已经分析出时不再分析
            if (category == "")
            {
                switch (TransactionAnalysis(tempString, ref category))
                {
                    case -1: return -1;
                    case 1: return 1;
                    default: break;
                }
            }
            List<string> FoundBrand = new List<string>();
            foreach (KeyValuePair<string, List<string>> pair in BrandAndItsAlias)
            {
                if (tempString.Contains(pair.Key.ToUpper()))
                    if (!FoundBrand.Contains(pair.Key.ToUpper()))
                        FoundBrand.Add(pair.Key.ToUpper());
                foreach (string value in pair.Value)
                    if (tempString.Contains(value.ToUpper()))
                        if (!FoundBrand.Contains(pair.Key.ToUpper()))
                            FoundBrand.Add(pair.Key.ToUpper());
            }
            if (FoundBrand.Count == 0)
                brand = "";
            else if (FoundBrand.Count == 1)
                brand = FoundBrand[0];
            else
            {
                FoundBrand.Sort(StringOperation.CompareStringByLength);
                if (FoundBrand.Contains("美菱") && FoundBrand.Contains("ING"))
                    brand = "美菱";
                else if (FoundBrand.Contains("广州樱花"))
                    brand = "WONDERFLOWER";
                else if (FoundBrand.Count > 1 && FoundBrand.Contains("厨宝"))
                {
                    FoundBrand.Remove("厨宝");
                    brand = FoundBrand[0];
                }
                else
                {
                    List<string> FoundModel = new List<string>();
                    foreach (Match match in Regex.Matches(tempString, @"([a-zA-Z0-9][^\u4e00-\u9fa5,]+[a-zA-Z0-9|)])+"))
                        FoundModel.Add(match.Value);
                    for (int m = 0; m < FoundModel.Count; m++)
                        for (int b = 0; b < FoundBrand.Count; b++)
                            if (FoundModel[m].Contains(FoundBrand[b]))
                                FoundBrand.Remove(FoundBrand[b--]);
                    if (FoundBrand.Count > 0)
                        brand = FoundBrand[0];
                }
            }
            return 0;
        }

        /// <summary>
        /// 分析字符串中的品类，返回-1分析失败;0分析成功;1分析成功但不包含需要的信息;2表示找到的品类超过1个，无法确定
        /// </summary>
        /// <param name="originalString">原始字符串</param>
        /// <param name="category">分析出的品类</param>
        /// <returns></returns>
        public int TransactionAnalysis(string originalString, ref string category)
        {
            //对原始字符串进行一次清洗
            string tempString = StringOperation.StringCleanUp(originalString);

            //原始字符串清洗后长度为0，返回分析失败
            if (tempString.Length == 0)
                return -1;

            List<string> FoundInvalidPhase = new List<string>();
            //搜索原始字符串中包含的无效中文
            foreach (string element in InvalidChinesePhaseList)
                if (tempString.Contains(element) && !FoundInvalidPhase.Contains(element))
                    FoundInvalidPhase.Add(element);
            //搜索原始字符串中包含的无效英文
            foreach (string element in InvalidEnglishPhaseList)
                if (tempString.Contains(element) && !FoundInvalidPhase.Contains(element))
                    FoundInvalidPhase.Add(element);

            string chineseExpression = @"([\u4e00-\u9fa5]{2,})+";//中文信息正则表达式
            List<string> chineseInfo = new List<string>();//存放中文信息结果 
            //通过正则表达式截取清洗后的原始字符串中的中文字符串
            foreach (Match match in Regex.Matches(tempString, chineseExpression))
                chineseInfo.Add(match.Value);
            //截取到的中文字符串个数少于1个，返回分析失败
            if (chineseInfo.Count < 1)
                return -1;
            //为截取到的中文字符串集合按长度排序
            if (chineseInfo.Count > 1)
                chineseInfo.Sort(StringOperation.CompareStringByLength);

            List<string> categories = new List<string>();
            //从截取到的中文字符串集合中寻找品类
            foreach (string str in chineseInfo)
            {
                string temps = str;
                foreach (string key in PhaseToCategory.Keys)
                    if (temps.Contains(key))
                    {
                        //判断是不是未确定的字符
                        if (PhaseToCategory[key] != "其他" && PhaseToCategory[key] != "其它" && PhaseToCategory[key] != "未确定" && !categories.Contains(PhaseToCategory[key]))
                            categories.Add(PhaseToCategory[key]);
                        else
                            temps = temps.Replace(key, "");
                    }
            }

            //如果搜索到的无效词语个数不为0且搜索到的品类个数为0，返回分析成功，数据无效应舍弃
            if (FoundInvalidPhase.Count != 0 && categories.Count == 0)
                return 1;
            switch (categories.Count)
            {
                case 0:
                    //未找到品类，且未找到无效词语，返回分析失败
                    return -1;
                case 1:
                    //找到品类，返回分析成功
                    category = categories[0];
                    return 0;
                //找到多个疑似品类，返回分析失败，有多个疑似品类
                default:
                    if (categories.Contains("厨电套餐"))
                    {
                        category = "厨电套餐";
                        return 0;
                    }
                    return 2;
            }
        }

        //-------------------------------------------------------------------------------------------------------------------------
        public object[] TransactionAnalysis1(string originalString, string brand, string category, string Model)
        {
            //对原始字符串进行一次清洗
            string tempString = StringOperation.StringCleanUp(originalString).ToUpper();
            //原始字符串清洗后长度为0，返回分析失败
            if (tempString.Length == 0)
                return new object[] { -1, "", category, "" };
            //机型正则表达式            
            string ModelExpression = @"([a-zA-Z0-9][^\u4e00-\u9fa5,]+[a-zA-Z0-9|)])+";
            //中文信息正则表达式
            //string chineseExpression = @"([\u4e00-\u9fa5]{2,})+";
            //全英文信息
            //string engExpression = @"^[a-zA-Z]+$";
            //存放中文信息结果
            List<string> chieseInfo = new List<string>();
            bool categoryFound = false;
            //当没有从文件名或表名获得品类信息时进行品类分析
            if (category == null || category == "")
            {
                object[] ret = TransactionAnalysis1(tempString, category);
                switch (ret[0].ToString())
                {
                    case "1": return new object[] { 1, "", "", "" };
                    case "0":
                        category = ret[1].ToString();
                        categoryFound = true;
                        break;
                    default: 
                        break;
                }
            }
            else
                categoryFound = true;

            List<string> ModelInfo = new List<string>();//存放型号信息     
            //通过正则表达式截取原始字符串中的商家机型
            foreach (Match match in Regex.Matches(tempString, ModelExpression))
                if (!ModelInfo.Contains(match.Value))
                    ModelInfo.Add(match.Value);

            //判断字符串中应成对出现的符号，如（）只出现单个符号时分隔字符串
            //暂时只考虑字符串中只出现一个(的情况，其他情况待扩展功能
            for (int i = 0; i < ModelInfo.Count; i++)
                ModelInfo[i] = StringOperation.StringFilter(ModelInfo[i], tempString);//过滤通过正则表达式取出的型号中信息

            //去除商家机型中的特殊字符串，例如1.5L，1.5P等
            for (int i = 0; i < ModelInfo.Count; i++)
                if (specialNeedless.Contains(ModelInfo[i]))
                    ModelInfo.RemoveAt(i--);

            //得不到型号信息时跳过此条记录的分析（目前要求找不到机型的商品名称分析其是否是净水配件，故取消此项约束，14年12月19日zgl）
            //if (ModelInfo.Count < 1)
            //    return 1;

            List<string> FoundBrand = new List<string>();//存放品牌信息       
            //当没有从文件名或表名获得品牌信息时进行品牌分析
            if (brand.Length == 0)
            {
                if (categoryFound)
                    tempString = tempString.Replace(category, "");

                //取出品牌信息
                foreach (KeyValuePair<string, List<string>> pair in BrandAndItsAlias)
                {
                    if (tempString.Contains(pair.Key.ToUpper()))
                        if (!FoundBrand.Contains(pair.Key.ToUpper()))
                            FoundBrand.Add(pair.Key.ToUpper());
                    foreach (string value in pair.Value)
                        if (tempString.Contains(value.ToUpper()))
                            if (!FoundBrand.Contains(pair.Key.ToUpper()))
                                FoundBrand.Add(pair.Key.ToUpper());
                }

                //在型号信息中替换掉品牌信息
                for (int i = 0; i < ModelInfo.Count; i++)
                    for (int j = 0; j < FoundBrand.Count; j++)
                        ModelInfo[i] = ModelInfo[i].Replace(FoundBrand[j] + "_", "");
            }
            else
                FoundBrand.Add(brand);

            //通过品牌品类型号查询数据库
            string tempBrand = string.Empty, tempModel = string.Empty, tempCategory = string.Empty;
            List<string> ModelQueryRes = new List<string>();//保存查询得到的标准机型
            int resultCount = 0;
            string ModelWithoutBrandeng = string.Empty;//没有英文品牌开头的型号字符串
            if (categoryFound)
            {
                //找不到品牌信息有型号信息时返回原始字符串，有可能是新品牌
                if (FoundBrand.Count < 1 && ModelInfo.Count > 0)
                    return new object[] { -1, "", category, "" };
                //找不到品牌信息且找不到型号信息，则跳过分析
                if (FoundBrand.Count < 1 && ModelInfo.Count < 1)
                    return new object[] { 1, "", "", "" };
                foreach (string brandstr in FoundBrand)
                    foreach (string ModelStr in ModelInfo)
                    {
                        ModelWithoutBrandeng = ModelStr;
                        //如果机型以品牌的英文开头，去除品牌英文
                        if (BrandAndItsAlias.ContainsKey(brandstr))
                        {
                            foreach (string alias in BrandAndItsAlias[brandstr])
                                if (ModelStr.ToUpper().StartsWith(alias))
                                    ModelWithoutBrandeng = ModelWithoutBrandeng.Remove(0, alias.Length).Trim();
                        }
                        ModelWithoutBrandeng = Regex.Match(ModelWithoutBrandeng, ModelExpression).ToString();

                        //尝试查询由当前品类、品牌、商家机型确定的机型信息
                        resultModelInfo = ModelQuery(ModelWithoutBrandeng, brandstr, category);

                        if (resultModelInfo.RealModel.Length != 0)
                        {
                            category = resultModelInfo.Category;
                            tempBrand = brandstr;
                            tempModel = resultModelInfo.RealModel;
                            if (!ModelQueryRes.Contains(tempModel))
                                ModelQueryRes.Add(tempModel);
                            ++resultCount;
                        }
                        else if (ModelWithoutBrandeng.ToUpper().StartsWith("LED-") || ModelWithoutBrandeng.ToUpper().StartsWith("LCD-"))
                        {
                            #region 如果机型以LED-、LCD-开始尝试去除LED-、LCD-后查找数据库（有文件形式：32寸LED-型号）
                            resultModelInfo = ModelQuery(ModelWithoutBrandeng.Remove(0, 4), brandstr, category);
                            if (resultModelInfo.RealModel.Length != 0)
                            {
                                category = resultModelInfo.Category;
                                tempBrand = brandstr;
                                tempModel = resultModelInfo.RealModel;
                                if (!ModelQueryRes.Contains(tempModel))
                                    ModelQueryRes.Add(tempModel);
                                ++resultCount;
                            }
                            #endregion
                        }
                        else if (ModelWithoutBrandeng.ToUpper().Contains("P") && ModelWithoutBrandeng.ToUpper().IndexOf("P") < 4)
                        {
                            #region 尝试去除型号中的1.5P，3P，2P(有文件家来福写成：1.5P型号)
                            int pIndex = ModelWithoutBrandeng.ToUpper().IndexOf("P") + 1;
                            resultModelInfo = ModelQuery(ModelWithoutBrandeng.Remove(0, pIndex), brandstr, category);
                            if (resultModelInfo.RealModel.Length != 0)
                            {
                                category = resultModelInfo.Category;
                                tempBrand = brandstr;
                                tempModel = resultModelInfo.RealModel;
                                if (!ModelQueryRes.Contains(tempModel))
                                    ModelQueryRes.Add(tempModel);
                                ++resultCount;
                            }
                            #endregion
                        }
                        else if (ModelWithoutBrandeng.Contains(" "))
                        {
                            #region 源字符串中包含**匹（如1.5匹）子串，而**（1.5）被截入ModelWithoutBrandeng，尝试替换后进行查找
                            string[] temp = ModelWithoutBrandeng.Split(' ');
                            float tran;
                            if (float.TryParse(temp[temp.Length - 1], out tran))
                            {
                                resultModelInfo = ModelQuery(ModelWithoutBrandeng.Replace(temp[temp.Length - 1] + " ", ""), brandstr, category);
                                if (resultModelInfo.RealModel.Length != 0)
                                {
                                    category = resultModelInfo.Category;
                                    tempBrand = brandstr;
                                    tempModel = resultModelInfo.RealModel;
                                    if (!ModelQueryRes.Contains(tempModel))
                                        ModelQueryRes.Add(tempModel);
                                    ++resultCount;
                                }
                            }
                            #endregion
                        }

                    }

                if (ModelQueryRes.Count == 1)
                {
                    return new object[] { 0, tempBrand, category, ModelQueryRes[0] };
                }
                else
                {
                    if (FoundBrand.Count == 1 && (category == "净水器" || category == "饮水机") && (tempString.Contains("配件") || tempString.Contains("滤芯") || tempString.Contains("水龙头")))
                    {
                        brand = FoundBrand[0];
                        Model = "净水配件";
                        return new object[] { 0, FoundBrand[0], category, "净水配件" };
                    }
                }
                return new object[] { -1, "", category, "" };
            }

            //如果未找到品类
            foreach (string brandstr in FoundBrand)
                foreach (string ModelStr in ModelInfo)
                {
                    ModelWithoutBrandeng = ModelStr;
                    resultModelInfo = ModelQuery(ModelWithoutBrandeng, brandstr, string.Empty);
                    if (resultModelInfo.RealModel.Length != 0)
                    {
                        category = resultModelInfo.Category;
                        tempBrand = brandstr;
                        tempModel = resultModelInfo.RealModel;
                        tempCategory = resultModelInfo.Category;
                        if (!ModelQueryRes.Contains(tempModel))
                            ModelQueryRes.Add(tempModel);
                        ++resultCount;
                    }
                    else if (ModelWithoutBrandeng.ToUpper().StartsWith("LED-") || ModelWithoutBrandeng.ToUpper().StartsWith("LCD-"))
                    {
                        #region 如果机型以LED-开始尝试去除LED-后查找数据库（有文件形式：32寸LED-型号）
                        resultModelInfo = ModelQuery(ModelWithoutBrandeng.Remove(0, 4), brandstr, string.Empty);
                        if (resultModelInfo.RealModel.Length != 0)
                        {
                            category = resultModelInfo.Category;
                            tempBrand = brandstr;
                            tempModel = resultModelInfo.RealModel;
                            tempCategory = resultModelInfo.Category;
                            if (!ModelQueryRes.Contains(tempModel))
                                ModelQueryRes.Add(tempModel);
                            ++resultCount;
                        }
                        #endregion
                    }
                    else if (ModelWithoutBrandeng.ToUpper().Contains("P") && ModelWithoutBrandeng.ToUpper().IndexOf("P") < 4)
                    {
                        #region 尝试去除型号中的1.5P,3P,2P(有文件家来福写成：1.5P型号)
                        int pIndex = ModelWithoutBrandeng.ToUpper().IndexOf("P") + 1;
                        resultModelInfo = ModelQuery(ModelWithoutBrandeng.Remove(0, pIndex), brandstr, string.Empty);
                        if (resultModelInfo.RealModel.Length != 0)
                        {
                            category = resultModelInfo.Category;
                            tempBrand = brandstr;
                            tempModel = resultModelInfo.RealModel;
                            tempCategory = resultModelInfo.Category;
                            if (!ModelQueryRes.Contains(tempModel))
                                ModelQueryRes.Add(tempModel);
                            ++resultCount;
                        }
                        #endregion
                    }
                    else if (ModelWithoutBrandeng.Contains(" "))
                    {
                        #region 源字符串中包含**匹（如1.5匹）子串，而**（1.5）被截入ModelWithoutBrandeng，尝试替换后进行查找
                        string[] temp = ModelWithoutBrandeng.Split(' ');
                        float tran;
                        if (float.TryParse(temp[temp.Length - 1],out tran))
                        {
                            resultModelInfo = ModelQuery(ModelWithoutBrandeng.Replace(temp[temp.Length - 1] + " ", ""), brandstr, category);
                            if (resultModelInfo.RealModel.Length != 0)
                            {
                                category = resultModelInfo.Category;
                                tempBrand = brandstr;
                                tempModel = resultModelInfo.RealModel;
                                if (!ModelQueryRes.Contains(tempModel))
                                    ModelQueryRes.Add(tempModel);
                                ++resultCount;
                            }
                        }
                        #endregion
                    }
                }

            if (ModelQueryRes.Count == 1)
            {
                return new object[] { 0, tempBrand, tempCategory, ModelQueryRes[0] };
            }
            return new object[] { -1, "", category, "" };
        }


        public object[] TransactionAnalysis1(string originalString, string brand, string category)
        {
            //对原始字符串进行一次清洗
            string tempString = StringOperation.StringCleanUp(originalString).ToUpper();

            //原始字符串清洗后长度为0，返回分析失败
            if (tempString.Length == 0)
                return new object[] { -1, brand,category };

            //品类信息在外部已经分析出时不再分析
            if (category == null || category == "")
            {
                object[] ret = TransactionAnalysis1(tempString, category);
                switch (ret[0].ToString())
                {
                    case "-1": return new object[] { -1, brand, category };
                    case "1": return new object[] { 1, "", "" };
                    case "0":
                        category = ret[1].ToString();
                        break;
                    default: 
                        break;
                }
            }
            List<string> FoundBrand = new List<string>();
            foreach (KeyValuePair<string, List<string>> pair in BrandAndItsAlias)
            {
                if (tempString.Contains(pair.Key.ToUpper()))
                    if (!FoundBrand.Contains(pair.Key.ToUpper()))
                        FoundBrand.Add(pair.Key.ToUpper());
                foreach (string value in pair.Value)
                    if (tempString.Contains(value.ToUpper()))
                        if (!FoundBrand.Contains(pair.Key.ToUpper()))
                            FoundBrand.Add(pair.Key.ToUpper());
            }
            if (FoundBrand.Count == 0)
                brand = "";
            else if (FoundBrand.Count == 1)
                brand = FoundBrand[0];
            else
            {
                FoundBrand.Sort(StringOperation.CompareStringByLength);
                if (FoundBrand.Contains("美菱") && FoundBrand.Contains("ING"))
                    brand = "美菱";
                else if (FoundBrand.Contains("广州樱花"))
                    brand = "WONDERFLOWER";
                else if (FoundBrand.Count > 1 && FoundBrand.Contains("厨宝"))
                {
                    FoundBrand.Remove("厨宝");
                    brand = FoundBrand[0];
                }
                else
                {
                    List<string> FoundModel = new List<string>();
                    foreach (Match match in Regex.Matches(tempString, @"([a-zA-Z0-9][^\u4e00-\u9fa5,]+[a-zA-Z0-9|)])+"))
                        FoundModel.Add(match.Value);
                    for (int m = 0; m < FoundModel.Count; m++)
                        for (int b = 0; b < FoundBrand.Count; b++)
                            if (FoundModel[m].Contains(FoundBrand[b]))
                                FoundBrand.Remove(FoundBrand[b--]);
                    if (FoundBrand.Count > 0)
                        brand = FoundBrand[0];
                }
            }
            return new object[] { 0, brand, category };
        }


        public object[] TransactionAnalysis1(string originalString, string category)
        {
            //对原始字符串进行一次清洗
            string tempString = StringOperation.StringCleanUp(originalString);

            //原始字符串清洗后长度为0，返回分析失败
            if (tempString.Length == 0)
                return new object[] { -1, "" };

            List<string> FoundInvalidPhase = new List<string>();
            //搜索原始字符串中包含的无效中文
            foreach (string element in InvalidChinesePhaseList)
                if (tempString.Contains(element) && !FoundInvalidPhase.Contains(element))
                    FoundInvalidPhase.Add(element);
            //搜索原始字符串中包含的无效英文
            foreach (string element in InvalidEnglishPhaseList)
                if (tempString.Contains(element) && !FoundInvalidPhase.Contains(element))
                    FoundInvalidPhase.Add(element);

            string chineseExpression = @"([\u4e00-\u9fa5]{2,})+";//中文信息正则表达式
            List<string> chineseInfo = new List<string>();//存放中文信息结果 
            //通过正则表达式截取清洗后的原始字符串中的中文字符串
            foreach (Match match in Regex.Matches(tempString, chineseExpression))
                chineseInfo.Add(match.Value);
            //截取到的中文字符串个数少于1个，返回分析失败
            if (chineseInfo.Count < 1)
                return new object[] { -1, "" };
            //为截取到的中文字符串集合按长度排序
            if (chineseInfo.Count > 1)
                chineseInfo.Sort(StringOperation.CompareStringByLength);

            List<string> categories = new List<string>();
            //从截取到的中文字符串集合中寻找品类
            foreach (string str in chineseInfo)
            {
                string temps = str;
                foreach (string key in PhaseToCategory.Keys)
                    if (temps.Contains(key))
                    {
                        //判断是不是未确定的字符
                        if (PhaseToCategory[key] != "其他" && PhaseToCategory[key] != "其它" && PhaseToCategory[key] != "未确定" && !categories.Contains(PhaseToCategory[key]))
                            categories.Add(PhaseToCategory[key]);
                        else
                            temps = temps.Replace(key, "");
                    }
            }

            //如果搜索到的无效词语个数不为0且搜索到的品类个数为0，返回分析成功，数据无效应舍弃
            if (FoundInvalidPhase.Count != 0 && categories.Count == 0)
                return new object[] { 1, "" };
            switch (categories.Count)
            {
                case 0:
                    //未找到品类，且未找到无效词语，返回分析失败
                    return new object[] { -1, category };
                case 1:
                    //找到品类，返回分析成功
                    return new object[] { 0, categories[0] };
                //找到多个疑似品类，返回分析失败，有多个疑似品类
                default:
                    if (categories.Contains("厨电套餐"))
                    {
                        return new object[] { 0, "厨电套餐" };
                    }
                    return new object[] { 2, "" };
            }
        }
        //-------------------------------------------------------------------------------------------------------------------------


        /// <summary>
        /// 在URL库里用商品名称查询品类，返回-1分析失败;0分析成功;1分析成功但不包含需要的信息;2表示找到的品类超过1个，无法确定
        /// </summary>
        /// <param name="originalString">原始字符串</param>
        /// <param name="category">分析出的品类</param>
        /// <returns></returns>
        public int QueryCategoryFromURLStore(string originalString, ref string category)
        {
            //对原始字符串进行一次清洗
            string tempString = StringOperation.StringCleanUp(originalString);
            //原始字符串清洗后长度为0，返回分析失败
            if (tempString.Length == 0)
                return -1;
            URLStoreInitializing();
            if (URLStore == null || URLStore.Rows.Count > 0)
                return -1;
            try
            {
                string[] cats = (from p in URLStore.AsEnumerable() where p.Field<string>("商品名称") == originalString select p.Field<string>("品类")).ToArray();
                if (cats.Length == 1)
                {
                    category = cats[0];
                    return 0;
                }
            }
            catch
            {
                return -1;
            }
            return -1;
        }


        /// <summary>
        /// 分析字符串中的品类
        /// 返回true时，若category不为空字符串则表示该品类属于系统定义需要的品类，否则表示未能分析出品类，不能确定是否需要
        /// 返回false则表示该数据不属于系统定义需要的品类
        /// </summary>
        /// <param name="originalString">原始字符串</param>
        /// <returns></returns>
        public bool AnalysisCategory(string originalString, out string category)
        {
            //对原始字符串进行一次格式清洗
            string tempString = StringOperation.StringCleanUp(originalString);
            category = string.Empty;
            //原始字符串清洗后长度为0，返回分析失败
            if (tempString.Length == 0)
                return true;
            List<string> FoundInvalidPhase = new List<string>();
            //搜索原始字符串中包含的无效中文
            foreach (string element in InvalidChinesePhaseList)
                if (tempString.Contains(element) && !FoundInvalidPhase.Contains(element))
                    FoundInvalidPhase.Add(element);
            //搜索原始字符串中包含的无效英文
            foreach (string element in InvalidEnglishPhaseList)
                if (tempString.Contains(element) && !FoundInvalidPhase.Contains(element))
                    FoundInvalidPhase.Add(element);

            //中文信息正则表达式
            string chineseExpression = @"([\u4e00-\u9fa5]{2,})+";
            //存放中文信息结果 
            List<string> chineseInfo = new List<string>();
            //通过正则表达式截取清洗后的原始字符串中的中文字符串
            foreach (Match match in Regex.Matches(tempString, chineseExpression))
                chineseInfo.Add(match.Value);
            //截取到的中文字符串个数少于1个，返回分析失败
            if (chineseInfo.Count < 1)
                return true;
            //为截取到的中文字符串集合按长度排序
            if (chineseInfo.Count > 1)
                chineseInfo.Sort(StringOperation.CompareStringByLength);

            List<string> categories = new List<string>();
            //从截取到的中文字符串集合中寻找品类
            foreach (string str in chineseInfo)
            {
                string temps = str;
                foreach (string key in PhaseToCategory.Keys)
                    if (temps.Contains(key))
                    {
                        //判断是不是未确定的字符
                        if (PhaseToCategory[key] != "其他" && PhaseToCategory[key] != "其它" && PhaseToCategory[key] != "未确定" && !categories.Contains(PhaseToCategory[key]))
                            categories.Add(PhaseToCategory[key]);
                        else
                            temps = temps.Replace(key, "");
                    }
            }

            //如果搜索到的无效词语个数不为0且搜索到的品类个数为0，返回分析成功，数据无效应舍弃
            if (FoundInvalidPhase.Count != 0 && categories.Count == 0)
                return false;
            switch (categories.Count)
            {
                case 0:
                    //未找到品类，且未找到无效词语，返回分析失败
                    return true;
                case 1:
                    //找到品类，返回分析成功
                    category = categories[0];
                    return true;
                //找到多个疑似品类，返回分析失败，有多个疑似品类
                default:
                    return false;
            };
        }

        public bool WorkOutCBMfromSPName(string nameStr, ref string brand, ref string category, ref string model)
        {
            //品类必有且正确
            if (nameStr == null || nameStr.Trim().Length == 0)
                return false;
            //处理参数
            nameStr = StringOperation.StringCleanUp(nameStr).ToUpper();
            brand = StringOperation.StringCleanUp(brand == null ? "" : brand).ToUpper();
            category = (category == null ? "" : category).Trim().ToUpper();
            model = StringOperation.StringCleanUp(model == null ? "" : model).ToUpper();

            InitialdtCategoryBrandModel(category);
            //分析品牌
            List<string> brandsDiscovered = new List<string>();
            if (brand.Length == 0)
            {
                foreach (DataRow item in drsMyCategoryBrandModel)
                    if (nameStr.Contains(item["品牌"].ToString()) && !brandsDiscovered.Contains(item["品牌"].ToString()))
                        brandsDiscovered.Add(item["品牌"].ToString());
                if (brandsDiscovered.Count == 1)
                    brand = brandsDiscovered[0];
                else
                    brand = "";
            }
            else//分析出中文品牌
            {
                MatchCollection mcChinese = regChineseBrand.Matches(brand);
                foreach (Match match in mcChinese)
                {
                    var query = (from p in drsMyCategoryBrandModel.AsParallel() where p.Field<string>("品牌") == match.Value select p).Distinct();
                    if (query.Count() == 1 && !brandsDiscovered.Contains(match.Value))
                        brandsDiscovered.Add(match.Value);
                }
                if (brandsDiscovered.Count == 1)
                    brand = brandsDiscovered[0];
                else
                {
                    brandsDiscovered.Clear();
                    MatchCollection mcEnglish = regEnglishBrand.Matches(brand);
                    foreach (Match match in mcEnglish)
                    {
                        var query = (from p in drsMyCategoryBrandModel.AsParallel() where p.Field<string>("品牌") == match.Value select p).Distinct();
                        if (query.Count() == 1 && !brandsDiscovered.Contains(match.Value))
                            brandsDiscovered.Add(match.Value);
                    }
                    if (brandsDiscovered.Count == 1)
                        brand = brandsDiscovered[0];
                    else
                        brand = "";
                }
            }
            //分析机型,直接用正则匹配
            InitialBusinessModels(category);
            List<string> modelsDiscovered = new List<string>();
            MatchCollection mc = null;
            if (model.Length == 0)
            {
                mc = regEnglishModel.Matches(nameStr);
                if (mc.Count == 0)
                    model = "";
                else
                {
                    foreach (Match item in mc)
                    {
                        string c = category, b = brand, m = item.Value;
                        var query = (from p in drsMyBusinessModels.AsEnumerable() where p.Field<string>("品类") == c && p.Field<string>("品牌") == b && p.Field<string>("商家机型") == m select p).Distinct();
                        if (query.Count() == 1 && !modelsDiscovered.Contains(m))
                            modelsDiscovered.Add(m);
                    }
                    if (modelsDiscovered.Count == 1)
                        model = modelsDiscovered[0];
                    else
                    {
                        if (mc.Count == 1)
                            model = mc[0].Value;
                        else
                            model = "";
                    }
                }
            }
            else
            {
                string c = category, b = brand, m = model;
                var query = (from p in drsMyBusinessModels.AsEnumerable() where p.Field<string>("品类") == c && p.Field<string>("品牌") == b && p.Field<string>("商家机型") == m select p).Distinct();
                if (query.Count() == 1)
                    model = m;
                else
                    model = "";
            }
            return (category.Length > 0 && brand.Length > 0 && model.Length > 0);
        }



        /// <summary>
        /// 分析字符串中的品牌、品类、机型，返回-1分析失败;0分析成功包含需要的信息;1分析成功但不包含需要的信息
        /// </summary>
        /// <param name="originalString">原始字符串</param>
        /// <param name="brand">分析出的品牌</param>
        /// <param name="category">分析出的品类</param>
        /// <param name="Model">分析出的型号</param>
        /// <returns>测试阶段</returns>
        public int ShopDataAnalysis(string originalString, ref string brand, ref string category, ref string Model)
        {
            //对原始字符串进行一次清洗
            string tempString = StringOperation.StringCleanUp(originalString).ToUpper();
            //原始字符串清洗后长度为0，返回分析失败
            if (tempString.Length == 0)
                return -1;
            //机型正则表达式            
            string ModelExpression = @"([a-zA-Z0-9][^\u4e00-\u9fa5,]+[a-zA-Z0-9|)])+";
            //中文信息正则表达式
            //string chineseExpression = @"([\u4e00-\u9fa5]{2,})+";
            //全英文信息
            //string engExpression = @"^[a-zA-Z]+$";
            //存放中文信息结果
            List<string> chieseInfo = new List<string>();
            bool categoryFound = false;

            //新品类分析方法---用零售词汇表分析
            if (dtFenCi1 == null || dtFenCi1.Rows.Count == 0 || dtFenCi2 == null || dtFenCi2.Rows.Count == 0)
            {
                MSSQLExecute mysql = new MSSQLExecute(MyConfiguration.Source);
                var query = mysql.ExecuteScalar("select top 1 count(distinct 标准品类) from 新零售词汇表 group by 一级分词 order by count(distinct 标准品类) desc");
                if (Convert.ToInt32(query) > 1)
                    throw new Exception("存在一个[一级分词]对应一个以上[品类]的情况,请检查[新零售词汇表]");
                //var query1 = mysql.ExecuteScalar("select top 1 count(distinct 标准品类) from 新零售词汇表 group by 二级分词,删除分词 order by count(distinct 标准品类) desc");
                //if (Convert.ToInt32(query1) > 1)
                //    throw new Exception("存在一组[一级分词][删除分词]对应一个以上[品类]的情况,请检查[新零售词汇表]");
                dtFenCi1 = mysql.ExecuteQuery("select distinct 标准品类,一级分词 from 新零售词汇表");
                dtFenCi2 = mysql.ExecuteQuery("select distinct 标准品类,二级分词,isnull(删除分词,'')删除分词 from 新零售词汇表");
            }
            List<string> categoryfoundlist = new List<string>();
            foreach (DataRow dr in dtFenCi1.Rows)
                if (originalString.Contains(dr["一级分词"].ToString()))
                {
                    //categoryfoundlist.Add(dr["标准品类"].ToString());
                    category = dr["标准品类"].ToString();
                    categoryFound = true;
                    break;
                }
            if (!categoryFound)
            {
                categoryfoundlist.Clear();
                foreach (DataRow dr in dtFenCi2.Rows)
                    if (originalString.Contains(dr["二级分词"].ToString()))
                    {
                        if (!originalString.Contains(dr["删除分词"].ToString()))
                        {
                            category = dr["标准品类"].ToString();
                            categoryFound = true;
                            break;
                        }
                        else
                            continue;
                    }
            }

            List<string> ModelInfo = new List<string>();//存放型号信息     
            //通过正则表达式截取原始字符串中的商家机型
            foreach (Match match in Regex.Matches(tempString, ModelExpression))
                if (!ModelInfo.Contains(match.Value))
                    ModelInfo.Add(match.Value);

            //判断字符串中应成对出现的符号，如（）只出现单个符号时分隔字符串
            //暂时只考虑字符串中只出现一个(的情况，其他情况待扩展功能
            for (int i = 0; i < ModelInfo.Count; i++)
                ModelInfo[i] = StringOperation.StringFilter(ModelInfo[i], tempString);//过滤通过正则表达式取出的型号中信息


            //去除商家机型中的特殊字符串，例如1.5L，1.5P等
            for (int i = 0; i < ModelInfo.Count; i++)
                if (specialNeedless.Contains(ModelInfo[i]))
                    ModelInfo.RemoveAt(i--);

            List<string> FoundBrand = new List<string>();//存放品牌信息       
            //当没有从文件名或表名获得品牌信息时进行品牌分析
            if (brand.Length == 0)
            {
                if (categoryFound)
                    tempString = tempString.Replace(category, "");

                //取出品牌信息
                foreach (KeyValuePair<string, List<string>> pair in BrandAndItsAlias)
                {
                    if (tempString.Contains(pair.Key.ToUpper()))
                        if (!FoundBrand.Contains(pair.Key.ToUpper()))
                            FoundBrand.Add(pair.Key.ToUpper());
                    foreach (string value in pair.Value)
                        if (tempString.Contains(value.ToUpper()))
                            if (!FoundBrand.Contains(pair.Key.ToUpper()))
                                FoundBrand.Add(pair.Key.ToUpper());
                }

                //在型号信息中替换掉品牌信息
                for (int i = 0; i < ModelInfo.Count; i++)
                    for (int j = 0; j < FoundBrand.Count; j++)
                        ModelInfo[i] = ModelInfo[i].Replace(FoundBrand[j] + "_", "");
            }
            else
                FoundBrand.Add(brand);

            //通过品牌品类型号查询数据库
            string tempBrand = string.Empty, tempModel = string.Empty, tempCategory = string.Empty;
            List<string> ModelQueryRes = new List<string>();//保存查询得到的标准机型
            int resultCount = 0;
            string ModelWithoutBrandeng = string.Empty;//没有英文品牌开头的型号字符串
            if (categoryFound)
            {
                //找不到品牌信息有型号信息时返回原始字符串，有可能是新品牌
                if (FoundBrand.Count < 1 && ModelInfo.Count > 0)
                    return -1;
                //找不到品牌信息且找不到型号信息，则跳过分析
                if (FoundBrand.Count < 1 && ModelInfo.Count < 1)
                    return 1;
                foreach (string brandstr in FoundBrand)
                    foreach (string ModelStr in ModelInfo)
                    {
                        ModelWithoutBrandeng = ModelStr;
                        //如果机型以品牌的英文开头，去除品牌英文
                        if (BrandAndItsAlias.ContainsKey(brandstr))
                        {
                            foreach (string alias in BrandAndItsAlias[brandstr])
                                if (ModelStr.ToUpper().StartsWith(alias))
                                    ModelWithoutBrandeng = ModelWithoutBrandeng.Remove(0, alias.Length).Trim();
                        }
                        ModelWithoutBrandeng = Regex.Match(ModelWithoutBrandeng, ModelExpression).ToString();

                        //尝试查询由当前品类、品牌、商家机型确定的机型信息
                        resultModelInfo = ModelQuery(ModelWithoutBrandeng, brandstr, category);

                        if (resultModelInfo.RealModel.Length != 0)
                        {
                            category = resultModelInfo.Category;
                            tempBrand = brandstr;
                            tempModel = resultModelInfo.RealModel;
                            if (!ModelQueryRes.Contains(tempModel))
                                ModelQueryRes.Add(tempModel);
                            ++resultCount;
                        }
                        else if (ModelWithoutBrandeng.ToUpper().StartsWith("LED-") || ModelWithoutBrandeng.ToUpper().StartsWith("LCD-"))
                        {
                            #region 如果机型以LED-、LCD-开始尝试去除LED-、LCD-后查找数据库（有文件形式：32寸LED-型号）
                            resultModelInfo = ModelQuery(ModelWithoutBrandeng.Remove(0, 4), brandstr, category);
                            if (resultModelInfo.RealModel.Length != 0)
                            {
                                category = resultModelInfo.Category;
                                tempBrand = brandstr;
                                tempModel = resultModelInfo.RealModel;
                                if (!ModelQueryRes.Contains(tempModel))
                                    ModelQueryRes.Add(tempModel);
                                ++resultCount;
                            }
                            #endregion
                        }
                        else if (ModelWithoutBrandeng.ToUpper().Contains("P") && ModelWithoutBrandeng.ToUpper().IndexOf("P") < 4)
                        {
                            #region 尝试去除型号中的1.5P，3P，2P(有文件家来福写成：1.5P型号)
                            int pIndex = ModelWithoutBrandeng.ToUpper().IndexOf("P") + 1;
                            resultModelInfo = ModelQuery(ModelWithoutBrandeng.Remove(0, pIndex), brandstr, category);
                            if (resultModelInfo.RealModel.Length != 0)
                            {
                                category = resultModelInfo.Category;
                                tempBrand = brandstr;
                                tempModel = resultModelInfo.RealModel;
                                if (!ModelQueryRes.Contains(tempModel))
                                    ModelQueryRes.Add(tempModel);
                                ++resultCount;
                            }
                            #endregion
                        }
                        else if (ModelWithoutBrandeng.Contains(" "))
                        {
                            #region 源字符串中包含**匹（如1.5匹）子串，而**（1.5）被截入ModelWithoutBrandeng，尝试替换后进行查找
                            string[] temp = ModelWithoutBrandeng.Split(' ');
                            float tran;
                            if (float.TryParse(temp[temp.Length - 1], out tran))
                            {
                                resultModelInfo = ModelQuery(ModelWithoutBrandeng.Replace(temp[temp.Length - 1] + " ", ""), brandstr, category);
                                if (resultModelInfo.RealModel.Length != 0)
                                {
                                    category = resultModelInfo.Category;
                                    tempBrand = brandstr;
                                    tempModel = resultModelInfo.RealModel;
                                    if (!ModelQueryRes.Contains(tempModel))
                                        ModelQueryRes.Add(tempModel);
                                    ++resultCount;
                                }
                            }
                            #endregion
                        }

                    }

                if (ModelQueryRes.Count == 1)
                {
                    brand = tempBrand;
                    Model = ModelQueryRes[0];
                    return 0;
                }
                else
                {
                    if (FoundBrand.Count == 1 && (category == "净水器" || category == "饮水机") && (tempString.Contains("配件") || tempString.Contains("滤芯") || tempString.Contains("水龙头")))
                    {
                        brand = FoundBrand[0];
                        Model = "净水配件";
                        return 0;
                    }
                }
                return -1;
            }

            //如果未找到品类
            foreach (string brandstr in FoundBrand)
                foreach (string ModelStr in ModelInfo)
                {
                    ModelWithoutBrandeng = ModelStr;
                    resultModelInfo = ModelQuery(ModelWithoutBrandeng, brandstr, string.Empty);
                    if (resultModelInfo.RealModel.Length != 0)
                    {
                        category = resultModelInfo.Category;
                        tempBrand = brandstr;
                        tempModel = resultModelInfo.RealModel;
                        tempCategory = resultModelInfo.Category;
                        if (!ModelQueryRes.Contains(tempModel))
                            ModelQueryRes.Add(tempModel);
                        ++resultCount;
                    }
                    else if (ModelWithoutBrandeng.ToUpper().StartsWith("LED-") || ModelWithoutBrandeng.ToUpper().StartsWith("LCD-"))
                    {
                        #region 如果机型以LED-开始尝试去除LED-后查找数据库（有文件形式：32寸LED-型号）
                        resultModelInfo = ModelQuery(ModelWithoutBrandeng.Remove(0, 4), brandstr, string.Empty);
                        if (resultModelInfo.RealModel.Length != 0)
                        {
                            category = resultModelInfo.Category;
                            tempBrand = brandstr;
                            tempModel = resultModelInfo.RealModel;
                            tempCategory = resultModelInfo.Category;
                            if (!ModelQueryRes.Contains(tempModel))
                                ModelQueryRes.Add(tempModel);
                            ++resultCount;
                        }
                        #endregion
                    }
                    else if (ModelWithoutBrandeng.ToUpper().Contains("P") && ModelWithoutBrandeng.ToUpper().IndexOf("P") < 4)
                    {
                        #region 尝试去除型号中的1.5P,3P,2P(有文件家来福写成：1.5P型号)
                        int pIndex = ModelWithoutBrandeng.ToUpper().IndexOf("P") + 1;
                        resultModelInfo = ModelQuery(ModelWithoutBrandeng.Remove(0, pIndex), brandstr, string.Empty);
                        if (resultModelInfo.RealModel.Length != 0)
                        {
                            category = resultModelInfo.Category;
                            tempBrand = brandstr;
                            tempModel = resultModelInfo.RealModel;
                            tempCategory = resultModelInfo.Category;
                            if (!ModelQueryRes.Contains(tempModel))
                                ModelQueryRes.Add(tempModel);
                            ++resultCount;
                        }
                        #endregion
                    }
                    else if (ModelWithoutBrandeng.Contains(" "))
                    {
                        #region 源字符串中包含**匹（如1.5匹）子串，而**（1.5）被截入ModelWithoutBrandeng，尝试替换后进行查找
                        string[] temp = ModelWithoutBrandeng.Split(' ');
                        float tran;
                        if (float.TryParse(temp[temp.Length - 1], out tran))
                        {
                            resultModelInfo = ModelQuery(ModelWithoutBrandeng.Replace(temp[temp.Length - 1] + " ", ""), brandstr, category);
                            if (resultModelInfo.RealModel.Length != 0)
                            {
                                category = resultModelInfo.Category;
                                tempBrand = brandstr;
                                tempModel = resultModelInfo.RealModel;
                                if (!ModelQueryRes.Contains(tempModel))
                                    ModelQueryRes.Add(tempModel);
                                ++resultCount;
                            }
                        }
                        #endregion
                    }
                }

            if (ModelQueryRes.Count == 1)
            {
                brand = tempBrand;
                Model = ModelQueryRes[0];
                category = tempCategory;
                return 0;
            }
            return -1;
        }


        #region cm
        //private readonly static string regChineseBrandStr = @"[\u4e00-\u9fa5]+";
        //private readonly static string regEnglishBrandStr = @"[a-zA-Z0-9-\\]+/{0,1}[a-zA-Z0-9]+";
        //private readonly static string regChineseEnglishBrandStr = @"[a-zA-Z0-9-\u4e00-\u9fa5\s\(\)]+";
        private static Regex regChineseBrand = new Regex(@"[\u4e00-\u9fa5]+", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);
        private static Regex regEnglishBrand = new Regex(@"[a-zA-Z0-9]+", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);
        private static Regex regEnglishModel = new Regex(@"[a-zA-Z0-9-\\/+]+(\([a-zA-Z0-9]{1,1}\))?", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);
        private static Regex regChineseEnglishModel = new Regex(@"[a-zA-Z0-9-\u4e00-\u9fa5\s\(\)]+", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);
        private static DataTable dtCategoryBrandModel;//公用的,静态
        private DataRow[] drsMyCategoryBrandModel;//相对于品类的,相对于实例的
        private void InitialdtCategoryBrandModel(string category)
        {
            MSSQLExecute mysql = new MSSQLExecute(MyConfiguration.Source);
            if (dtCategoryBrandModel == null || dtCategoryBrandModel.Rows.Count == 0)
            {
                dtCategoryBrandModel = mysql.ExecuteQuery("select distinct 品类,品牌,机型 from 型号表 order by 品类,品牌,机型");
            }
            if (drsMyCategoryBrandModel == null)
            {
                drsMyCategoryBrandModel = dtCategoryBrandModel.Select("品类 = '" + category + "'");
                return;
            }
            if (drsMyCategoryBrandModel.Length == 0)
            {
                drsMyCategoryBrandModel = dtCategoryBrandModel.Select("品类 = '" + category + "'");
                return;
            }
            var count = (from p in drsMyCategoryBrandModel.AsEnumerable() where p.Field<string>("品类") != category select p).Count();
            if (count > 0)
            {
                drsMyCategoryBrandModel = dtCategoryBrandModel.Select("品类 = '" + category + "'");
                return;
            }
        }
        private static DataTable dtBusinessModels;
        private DataRow[] drsMyBusinessModels;
        private DataTable dtFenCi1;
        private DataTable dtFenCi2;
        private void InitialBusinessModels(string category)
        {
            if (dtBusinessModels == null || dtBusinessModels.Rows.Count == 0)
            {
                MSSQLExecute mysql = new MSSQLExecute(MyConfiguration.Source);
                dtBusinessModels = mysql.ExecuteQuery("select distinct 品类,品牌,商家机型,机型 as 真实机型 from 商家型号对照表");
            }
            if (drsMyBusinessModels == null)
            {
                drsMyBusinessModels = dtBusinessModels.Select("品类 = '" + category + "'");
                return;
            }
            if (drsMyBusinessModels.Length == 0)
            {
                drsMyBusinessModels = dtBusinessModels.Select("品类 = '" + category + "'");
                return;
            }
            var count = (from p in drsMyBusinessModels.AsEnumerable() where p.Field<string>("品类") != category select p).Count();
            if (count > 0)
            {
                drsMyBusinessModels = dtBusinessModels.Select("品类 = '" + category + "'");
                return;
            }
        }
        #endregion
    }
}
