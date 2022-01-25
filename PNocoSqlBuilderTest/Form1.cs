using System;
using System.Collections.Generic;
using System.Windows.Forms;
using NPocoSqlBuilder;

namespace NPocoSqlBuilderTest
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, System.EventArgs e)
        {
            richTextBox1.Text = @"
SELECT t0.*,/**select**/
FROM dbo.[table01] t0
where /**where**/  
and /**where(test)**/
and /**where(DefaultSet)**/
and id = @0 and weight > @0 and weight < @1 
and f1=@namePara01
/**orderby**/
";
        }

        private void button2_Click(object sender, System.EventArgs e)
        {
            var sb = new SqlBuilder(new Dictionary<string, string>()
            {
                {"where(DefaultSet)", "1=2"}
            });

            var dic01 = new List<string>()
            {
                "string", "string", "int", "bool"
            };

            sb.Where("f1 in(@0)", dic01)
                .Where("f2 ='f2'")
                .Where("f3=@f3test",new {f3test="f3test"})
                .WhereNamed("test", "f2='where(test):'+@0", "whereNamedTest")
                .OrderBy("f1,f2,f3");

            var template=sb.AddTemplate(richTextBox1.Text, "para@01", "para@02", new {namePara01 = "namePara01"});

            richTextBox2.Text = template.RawSql;

            richTextBox2.AppendText(Environment.NewLine+"=======================================");
            var paras = template.Parameters;
            for (int i = 0; i < paras.Length; i++)
            {
                var para = paras[i];
                richTextBox2.AppendText(Environment.NewLine + $"ParaName:@{i}，value：{para ?? "null"}");
            }
        }
    }
}