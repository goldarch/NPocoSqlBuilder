using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NPocoSqlBuilder
{
    //dx ： 和Dapper.SqlBuilder代码重合度很高！
    //NPoco-master\NPoco-master\src\NPoco\SqlBuilder.cs
    //https://github.com/schotime/NPoco/blob/master/src/NPoco/SqlBuilder.cs
    /// <summary>
    /// 不要缓存sqlBuilder和temp！每次用都新建之，因为子句缓存和替换会带来不确定的因素
    /// </summary>
    public class SqlBuilder
    {
        #region 公共属性

        public bool ReuseParameters { get; set; }

        #endregion

        Dictionary<string, Clauses> data = new Dictionary<string, Clauses>();
        int _seq;

        class Clause
        {
            public string Sql { get; set; }
            public string ResolvedSql { get; set; }
            public List<object> Parameters { get; set; }
        }

        class Clauses : List<Clause>
        {
            string joiner;
            string prefix;
            string postfix;

            public Clauses(string joiner, string prefix, string postfix)
            {
                this.joiner = joiner;
                this.prefix = prefix;
                this.postfix = postfix;
            }

            public string ResolveClauses(List<object> finalParams, bool reuseParameters)
            {
                foreach (var item in this)
                {
                    item.ResolvedSql = ParameterHelper.ProcessParams(item.Sql, item.Parameters.ToArray(), finalParams,
                        reuseParameters);
                }

                return prefix + string.Join(joiner, this.Select(c => c.ResolvedSql).ToArray()) + postfix;
            }
        }

        /// <summary>
        /// Initialises the SqlBuilder
        /// </summary>
        public SqlBuilder()
        {
        }

        /// <summary>
        /// Initialises the SqlBuilder with default replacement overrides
        /// </summary>
        /// <param name="defaultOverrides">A dictionary of token overrides. A value null means the token will not be replaced.</param>
        /// <example>
        /// { "where", "1=1" }
        /// { "where(name)", "1!=1" }
        /// </example>
        public SqlBuilder(Dictionary<string, string> defaultOverrides)
        {
            defaultsIfEmpty.InsertRange(0,
                defaultOverrides.Select(x => new KeyValuePair<string, string>(Regex.Escape(x.Key), x.Value)));
        }

        public Template AddTemplate(string sql, params object[] parameters)
        {
            return new Template(this, sql, parameters);
        }

        /// <summary>
        /// Adds a new SQL clause.  Also internally used by all other methods like Select, Where, Order, ...
        /// </summary>
        /// <param name="name">lower case name of the clause (eg select, where, ...) </param>
        /// <param name="sql"></param>
        /// <param name="parameters">for the sql string</param>
        /// <param name="joiner">The string which will be used to join multiple parts of the same clause. Remember to add whitespace before and after.</param>
        /// <param name="prefix"></param>
        /// <param name="postfix"></param>
        public void AddClause(string name, string sql, object[] parameters, string joiner, string prefix,
            string postfix)
        {
            Clauses clauses;
            if (!data.TryGetValue(name, out clauses))
            {
                clauses = new Clauses(joiner, prefix, postfix);
                data[name] = clauses;
            }

            clauses.Add(new Clause {Sql = sql, Parameters = new List<object>(parameters)});
            _seq++;
        }

        readonly List<KeyValuePair<string, string>> defaultsIfEmpty = new List<KeyValuePair<string, string>>()
        {
            new KeyValuePair<string, string>(@"where\([\w]+\)", "1=1"),
            new KeyValuePair<string, string>("where", "1=1"),
            new KeyValuePair<string, string>("select", "1")
        };

        /// <summary>
        /// Replaces the Select columns. Uses /**select**/
        /// </summary>
        public SqlBuilder Select(params string[] columns)
        {
            AddClause("select", string.Join(", ", columns), new object[] { }, ", ", "", "");
            return this;
        }

        /// <summary>
        /// Adds an Inner Join. Uses /**join**/
        /// </summary>
        public SqlBuilder Join(string sql, params object[] parameters)
        {
            AddClause("join", sql, parameters, "\nINNER JOIN ", "\nINNER JOIN ", "\n");
            return this;
        }

        /// <summary>
        /// Adds a Left Join. Uses /**leftjoin**/
        /// </summary>
        public SqlBuilder LeftJoin(string sql, params object[] parameters)
        {
            AddClause("leftjoin", sql, parameters, "\nLEFT JOIN ", "\nLEFT JOIN ", "\n");
            return this;
        }

        /// <summary>
        /// Adds a filter. The Where keyword still needs to be specified. Uses /**where**/
        /// 【注意】不会主动加where,“where /**where**/”
        /// </summary>
        public SqlBuilder Where(string sql, params object[] parameters)
        {
            AddClause("where", "( " + sql + " )", parameters, " AND ", "", "\n");
            return this;
        }

        /// <summary>
        /// Adds a named filter. The Where keyword still needs to be specified. Uses /**where(name)**/
        /// 【注意】不会主动加where,“where /**where**/”
        /// </summary>
        public SqlBuilder WhereNamed(string name, string sql, params object[] parameters)
        {
            AddClause("where(" + name + ")", "( " + sql + " )", parameters, " AND ", "", "\n");
            return this;
        }

        /// <summary>
        /// Adds an Order By clause. Uses /**orderby**/
        /// </summary>
        public SqlBuilder OrderBy(string sql, params object[] parameters)
        {
            AddClause("orderby", sql, parameters, ", ", "ORDER BY ", "\n");
            return this;
        }

        /// <summary>
        /// Adds columns in the Order By clause. Uses /**orderbycols**/
        /// </summary>
        public SqlBuilder OrderByCols(params string[] columns)
        {
            AddClause("orderbycols", string.Join(", ", columns), new object[] { }, ", ", ", ", "");
            return this;
        }

        /// <summary>
        /// Adds a Group By clause. Uses /**groupby**/
        /// </summary>
        public SqlBuilder GroupBy(string sql, params object[] parameters)
        {
            AddClause("groupby", sql, parameters, " , ", "\nGROUP BY ", "\n");
            return this;
        }

        /// <summary>
        /// Adds a Having clause. Uses /**having**/
        /// </summary>
        public SqlBuilder Having(string sql, params object[] parameters)
        {
            AddClause("having", sql, parameters, "\nAND ", "HAVING ", "\n");
            return this;
        }

        /// <summary>
        /// Template虽然是处于SqlBuilder内部，但是输入和输出都是在template中进行！反而SqlBuilder只是一个处理的内部机器
        /// 【注意，由于处理过程，参数会缓存,所以，每次处理都新建build和template,不要用历史的！】
        /// </summary>
        public class Template
        {
            #region 公共属性

            public bool TokenReplacementRequired { get; set; }

            public string RawSql
            {
                get
                {
                    ResolveSql();
                    return _rawSql;
                }
            }

            public object[] Parameters
            {
                get
                {
                    ResolveSql();
                    return finalParams.ToArray();
                }
            }

            #endregion


            readonly string sql;

            readonly SqlBuilder builder;

            //final 最后
            //这里是一次处理的全部参数的集合！
            private List<object> finalParams = new List<object>();
            int dataSeq;

            string _rawSql;

            public Template(SqlBuilder builder, string sql, params object[] parameters)
            {
                this.builder = builder;
                this.sql = ParameterHelper.ProcessParams(sql, parameters, finalParams, builder.ReuseParameters);
            }

            //dx，注意，/**where**/点位符一定要保证处理！因为如果忘记处理，执行语句时，会把其当成一个注释块！！
            //"delete table /**where**/" 如果变成了"delete table /*注释*/"可能是灾难了！
            static Regex regex = new Regex(@"(\/\*\*[^*/]+\*\*\/)", RegexOptions.Compiled | RegexOptions.Multiline);

            void ResolveSql()
            {
                if (dataSeq != builder._seq)
                {
                    _rawSql = sql;
                    foreach (var pair in builder.data)
                    {
                        _rawSql = _rawSql.Replace("/**" + pair.Key + "**/",
                            pair.Value.ResolveClauses(finalParams, builder.ReuseParameters));
                    }

                    ReplaceDefaults();

                    dataSeq = builder._seq;
                }

                if (builder._seq == 0)
                {
                    _rawSql = sql;
                    ReplaceDefaults();
                }
            }

            private void ReplaceDefaults()
            {
                if (TokenReplacementRequired)
                {
                    foreach (var pair in builder.defaultsIfEmpty)
                    {
                        var fullToken = GetFullTokenRegexPattern(pair.Key);
                        if (Regex.IsMatch(_rawSql, fullToken))
                        {
                            throw new Exception(string.Format(
                                "Token '{0}' not used. All tokens must be replaced if TokenReplacementRequired switched on.",
                                fullToken));
                        }
                    }
                }

                _rawSql = regex.Replace(_rawSql, x =>
                {
                    var token = x.Groups[1].Value;
                    var found = false;

                    foreach (var pair in builder.defaultsIfEmpty)
                    {
                        var fullToken = GetFullTokenRegexPattern(pair.Key);
                        if (Regex.IsMatch(token, fullToken))
                        {
                            if (pair.Value != null)
                            {
                                token = Regex.Replace(token, fullToken, " " + pair.Value + " ");
                            }

                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        token = string.Empty;
                    }

                    return token;
                });
            }

            private static string GetFullTokenRegexPattern(string key)
            {
                return @"/\*\*" + key + @"\*\*/";
            }
        }
    }
}