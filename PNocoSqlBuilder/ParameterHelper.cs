using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NPocoSqlBuilder
{
    /*重要总结

    NPoco中的原始参数，都是占位符，因为最后都要被替换掉！！并且针对一些特殊的枚举类型，最后可以出并列处理,如:in(@1,@2,@3)
    NPoco的参数是以“子句”作为“小作用域”的，子句内部参数命名具有一致性。
    每个子句（template也是一个大的子句），参数必须具有完整性，子句内不能少，不跨子句找参数
    NPoco对代码中本身存在的@处理不好，要注意，防止被替换！对包含@的语句（非参数值）可以用一个点位符，等所有的处理完成后，再换回来！
    如硬编码：t1='@test'，这个@test是会被替换掉的！由于语句是硬编码，从一开始就有预见性，只要注意处理就不会有问题！

    UniqueNamespace.SqlBuilder中的参数是真的参数！
    UniqueNamespace的参数是全局一致性的！各个子句虽然也加参数，只不过是往大参数池中迭代加入，和子句没有太大关系！比如在template中一次性加入也没有错！

     */

    //代码来自PNoco ParameterHelper等
    // Helper to handle named parameters from object properties
    //帮助处理来自对象属性的命名参数
    //ParameterHelper.cs
    //D:\DaxERP-Library\NPoco 含sqlbuilder 全版本支持\NPoco-master\NPoco-master\src\NPoco\ParameterHelper.cs
    //测试： D:\DaxERP-Library\NPoco 含sqlbuilder 全版本支持\NPoco-master\NPoco-master\test\NPoco.Tests\ParameterHelper.cs
    /// <summary>
    /// 对参数的子句处理与全局处理
    /// </summary>
    /// sql中如果有显式的@符号，处理会不正常(见测试：D:\DaxERP-Library\NPoco 含sqlbuilder 全版本支持\NPoco-master\NPoco-master\dxtest\)
    //AutoIncrementParameter
    public static class ParameterHelper
    {
        private static readonly Regex RxParamsPrefix = new Regex(@"(?<!@)@\w+", RegexOptions.Compiled);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="argsSrc"></param>
        /// <param name="argsDest"></param>
        /// <param name="reuseParameters">跨子句“值”复用。子句内同名参数是复用的</param>
        /// <returns></returns>
        public static string ProcessParams(string sql, object[] argsSrc, List<object> argsDest,
            bool reuseParameters = false)
        {
            //子句域内部临时参数列表，用于检查子句域的同名参数复用（相对应的，argsDest是全域参数列表）！
            var parameters = new Dictionary<string, string>();

            //注意：这里是迭代处理的！当有多个匹配项目，会多次执行！
            //parameters记录了多次执行的结果，用以临时检查是否有重复的Key值（即参数名称）！
            return RxParamsPrefix.Replace(sql, m =>
            {
                //m.Value是这样的：@id,@0等,可能会重复！比如sql子句有多个@1
                var matchValue = m.Value;

                //如果在一次处理中存在相同的参数名，则直接返回之前获取的值！
                if (parameters.TryGetValue(matchValue, out var paraName))
                    return paraName;

                //这里是二步合成了一步！
                //第一步非常重要，是“迭代字典的值”
                //item = parameters[m.Value] = ProcessParam(ref sql, m.Value, argsSrc, argsDest, reuseParameters);

                //==================================
                //dx:宁愿分两步来写，清晰！
                //第一步，新增一个key为m.Value的项目
                //第二步，返回
                //===================================
                //源：item = parameters[m.Value] = ProcessParam(ref sql, m.Value, args_src, args_dest, reuseParameters);
                //测试：item = parameters[m.Value] = ProcessParam(sql, m.Value, args_src, args_dest, reuseParameters);
                //dx:ref sql暂时没有看到ref的必要性，sql在调用方法中没有任何更改，但是否为以后可能的更改做准备？
                parameters[matchValue] = ProcessParam(ref sql, m.Value, argsSrc, argsDest, reuseParameters);
                paraName = parameters[matchValue];
                return paraName;
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="rawParamFullName">原始参数，包含@前缀</param>
        /// <param name="argsSrc">argsSrc 源</param>
        /// <param name="argsDest">destination 目的地</param>
        /// <param name="reuseParameters">reuse 复用 use again or more than once</param>
        /// <returns></returns>
        private static string ProcessParam(ref string sql, string rawParamFullName, object[] argsSrc,
            List<object> argsDest,
            bool reuseParameters)
        {
            //比如@1，从第二开始，即去掉符号@,
            string rawParamMainName = rawParamFullName.Substring(1);

            object argVal;

            //【功能】查找参数的值
            //进行参数判断，分"数字型参数"和“命名”
            //如果是数字参数，直接返回索引位置的值
            if (Int32.TryParse(rawParamMainName, out var paramIndex))
            {
                // Numbered parameter
                //数字参数的数值不能大于参数集合的总数,即如果总数最3，则只能是0,1,2
                //子语句都是从0开始，但最后，整体上是从0向上累加的！
                //语句可以被链接，并且每个新语句的参数从0开始。
                //1 sqlBuilder
                //2     .Where("height >= @0", 176)
                //3     .Where("weight > @0 and weight < @1", 30, 60);

                if (paramIndex < 0 || paramIndex >= argsSrc.Length)
                    throw new ArgumentOutOfRangeException(String.Format(
                        "Parameter '@{0}' specified but only {1} parameters supplied (in `{2}`)", paramIndex,
                        argsSrc.Length, sql));

                //【重要】数值参数必须全部为数值参数！因为直接“用数字当序号”从“参数源集合”中取值了！
                //如果子句中存在相同的值，
                //【重要约定】数字参数需要简单数据类型
                //[沒有蠢問題] c# 如何分辨 實值型別，參考型別 https://dotblogs.com.tw/initials/2016/06/04/223030
                //[C#] 基礎 - Value Type , Reference Type 用看記憶體內容 來測試 https://dotblogs.com.tw/initials/2017/01/28/A00_Basis
                //https://www.codeproject.com/Articles/1204612/How-string-Behaves-Like-Value-Type-as-it-is-refere
                //[C#] 基礎 - Value Type , Reference Type 用看記憶體內容 來測試 - 被 string 打臉 ?!! https://dotblogs.com.tw/initials/2021/04/26/101554
                //結論 string 是 參考型別 但是具有 實質型別 的行為
                //Type T = o.GetType();
                //bool isValueType = T.IsValueType;//值类型
                //bool isClass = T.IsClass;//类类型(对象类型)
                //bool isGenericType = T.IsGenericType;//泛型
                //bool isConstructedGenericType = T.IsConstructedGenericType;//对象为构造泛型类型
                //object oValue = 1; //值类型：IsValueType=True, IsClass=False
                //object oString = "字符串";//字符串：IsValueType=False, IsClass=True, 需要多重判断(o is String)
                //object oObject = new StringBuilder();//对象：IsValueType=False, IsClass=True
                //object oGeneric = new List<Object>();//泛型
                //object oRef = oGeneric;//引用类型(与被引用的对象测试一致）

                argVal = argsSrc[paramIndex];
            }
            else
            {
                // Look for a property on one of the arguments with this name
                //在具有此名称的参数之一上查找属性
                bool found = false;
                argVal = null;

                //1、字典类型，找到key，退出查找
                //2、非字典,判断属性名称，如果找到key，退出查找
                foreach (var o in argsSrc)
                {
                    //泛型（通用）IDictionary<TKey,TValue> Interface https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.idictionary-2?view=net-6.0
                    //表示键/值对的非泛型集合。
                    //由于IDictionary对象的每个元素都是键/值对，因此元素类型不是键的类型或值的类型。相反，元素类型是DictionaryEntry。
                    var dict = o as IDictionary;
                    if (dict != null)
                    {
                        //https://stackoverflow.com/questions/19503905/type-generictypearguments-property-vs-type-getgenericarguments-method
                        //C#中类型分析中的常见问题 Type https://www.cnblogs.com/yuanyuan/archive/2012/08/16/2642281.html
                        //Type.GetGenericArguments 方法 https://docs.microsoft.com/zh-cn/dotnet/api/system.type.getgenericarguments?view=net-6.0
                        //表示泛型类型的类型实参的 Type 对象的数组。 如果当前类型不是泛型类型，则返回一个空数组。
                        //范型GenericType反射要点 https://blog.csdn.net/lunasea0_0/article/details/6257395
                        //Type[] arguments = dict.GetType().GetGenericArguments();
                        Type type = dict.GetType();
                        Type[] arguments = type.GetGenericArguments();

                        //这里有一个约定，第一个参数需要是string，也就是对应命名参数的值！
                        if (arguments[0] == typeof(string))
                        {
                            //在键值集合中，以
                            var val = dict[rawParamMainName];
                            if (val != null)
                            {
                                found = true;
                                argVal = val;
                                break;
                            }
                        }
                    }

                    //属性名判断查找
                    var pi = o.GetType().GetProperty(rawParamMainName);
                    if (pi != null)
                    {
                        argVal = pi.GetValue(o, null);
                        found = true;
                        break;
                    }
                }

                if (!found)
                    throw new ArgumentException(String.Format(
                        "Parameter '@{0}' specified but none of the passed arguments have a property with this name (in '{1}')",
                        rawParamMainName, sql));
            }

            //【功能】对查找到的值进行2次处理，目前只针对IEnumerable类型的值进行了特别处理
            // Expand collections to parameter lists
            //将集合扩展到参数列表
            if ((argVal as System.Collections.IEnumerable) != null &&
                (argVal as string) == null &&
                (argVal as byte[]) == null)
            {
                var sb = new StringBuilder();
                foreach (var argItem in argVal as System.Collections.IEnumerable)
                {
                    //注意，源代码在此处没有进行reuseParameters的控制参数判断，直接从全域参数集合中查找重复项目了！
                    var indexOfExistingValue = argsDest.IndexOf(argItem);
                    //原代码：if (indexOfExistingValue >= 0) 未判断reuseParameters控制参数
                    if (reuseParameters && indexOfExistingValue >= 0)
                    {
                        sb.Append((sb.Length == 0 ? "@" : ",@") + indexOfExistingValue);
                    }
                    else
                    {
                        //数字化参数表达式（因为是先命名后新增，所以Count对应新增的索引）
                        sb.Append((sb.Length == 0 ? "@" : ",@") + argsDest.Count);
                        //【重要】维护全域参数值集合！
                        argsDest.Add(argItem);
                    }
                }


                //实际就是:((IEnumerable) argVal).Any() == false;
                if (sb.Length == 0)
                {
                    Type type = typeof(string);
                    var t = argVal.GetType();

                    //对“Array”和"IEnumerable<>"的
                    if (t.IsArray)
                        type = t.GetElementType();
                    else if (t.IsOrHasGenericInterfaceTypeOf(typeof(IEnumerable<>)))
                        type = t.GetGenericArguments().First();

                    //https://github.com/schotime/NPoco/issues/311
                    //adj. 双的，双重的；双数的；（飞机）双重控制的，复式操纵的；（定理、表达式等）对偶的
                    //从D:\DaxERP-Library\NPoco 含sqlbuilder 全版本支持\NPoco-master\NPoco-master\src\NPoco\DatabaseTypes\FirebirdDatabaseType.cs
                    //D:\DaxERP-Library\NPoco 含sqlbuilder 全版本支持\NPoco-master\NPoco-master\src\NPoco\DatabaseTypes\MySqlDatabaseType.cs
                    //public override void PreExecute(DbCommand cmd):
                    //cmd.CommandText = cmd.CommandText.Replace("/*poco_dual*/", "from RDB$DATABASE");
                    //cmd.CommandText = cmd.CommandText.Replace("/*poco_dual*/", "from dual");
                    //理解：的确体现了多重的控制，在特定的方言和数据库，/*poco_dual*/注释要转换成不同的内容（是不是有些数据库查询必须from???）！
                    //在mssql语句中，就真的只是一个注释
                    //形如：select @1 where 1=0
                    // /*poco_dual*/ 是注释，不是替换 

                    sb.AppendFormat($"select @{argsDest.Count} /*poco_dual*/ where 1 = 0");
                    //【重要】注意，此处维护“全域参数集合”（相对应的是子句域的参数值临时列表）！
                    //把“类型值”维护进“全域参数集合”的目的是什么？
                    argsDest.Add(GetDefault(type));
                }

                return sb.ToString();
            }
            else
            {
                if (reuseParameters)
                {
                    //复用，检查值！只要存在一样的值，不在于其表达的意义是否相同，就直接复用！
                    //比如1个人使用1个小时，正好是两个1，“数值复用” 
                    var indexOfExistingValue = argsDest.IndexOf(argVal);
                    if (indexOfExistingValue >= 0)
                        return "@" + indexOfExistingValue;
                }

                //不判断是否有相同值，直接新增一个参数！
                //新增的参数索引为:(总数值-1)
                argsDest.Add(argVal);
                return "@" + (argsDest.Count - 1).ToString();
            }
        }

        //MappingHelper.cs
        static object GetDefault(Type type)
        {
            if (type.GetTypeInfo().IsValueType)
            {
                return Activator.CreateInstance(type);
            }

            return null;
        }

        //TypeHelpers.cs
        static Type GetTypeInfo(this Type type)
        {
            return type;
        }

        //ReflectionUtils.cs
        static bool IsOrHasGenericInterfaceTypeOf(this Type type, Type genericTypeDefinition)
        {
            return type.GetTypeWithGenericTypeDefinitionOf(genericTypeDefinition) != null;
        }

        static Type GetTypeWithGenericTypeDefinitionOf(this Type type, Type genericTypeDefinition)
        {
            foreach (var t in type.GetInterfaces())
            {
                if (t.GetTypeInfo().IsGenericType && t.GetGenericTypeDefinition() == genericTypeDefinition)
                {
                    return t;
                }
            }

            var genericType = type.GetGenericType();
            if (genericType != null && genericType.GetGenericTypeDefinition() == genericTypeDefinition)
            {
                return genericType;
            }

            return null;
        }

        static Type GetGenericType(this Type type)
        {
            while (type != null)
            {
                if (type.GetTypeInfo().IsGenericType)
                    return type;

                type = type.GetTypeInfo().BaseType;
            }

            return null;
        }
    }
}