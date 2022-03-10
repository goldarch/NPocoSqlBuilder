# NPocoSqlBuilder
NPocoSqlBuilder 分享出来的独立模块，有详细注释  
  
.Net 4.0  
  
基于模板的SqlBuilder，NPoco修改于Dapper.SqlBuilder,但与Dapper.SqlBuilder在处理参数上有很大的不同！  并且有一些非常好的扩展。  
NPocoSqlBuilder的参数处理的基本单元是子句！经过筛选与处理的参数最终存入一个集合中。  

# 我认为NPocoSqlBuilder最大的好处是，变量的命名和编号，控制到了子句，不同字句间变量名不会冲突！
这就比较后期要插入sql语句的场景，不用考虑与已经存在的语句和子句之间的变量命名冲突，所以，更灵活！

相关资源：  
https://github.com/DapperLib/Dapper/tree/main/Dapper.SqlBuilder  
https://github.com/schotime/NPoco  
