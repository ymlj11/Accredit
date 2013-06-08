using System;
using System.Windows.Forms;
using FirebirdSql.Data.FirebirdClient;

namespace Accredit
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            encrypt = new Encrypt();
            filename = "accredit.lic";
            error = "";
        }

        Encrypt encrypt;
        String filename;
        String error;

        private void button1_Click(object sender, EventArgs e)
        {
            String company = this.textBox1.Text.Trim();
            DateTime start = this.dateTimePicker1.Value;
            DateTime end = this.dateTimePicker2.Value;

            if (company.Length == 0)
            {
                MessageBox.Show("请输入正确的公司名称!", "", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (end <= start)
            {
                MessageBox.Show("输入的有效日期不正确!", "", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            else
            {
                TimeSpan span = end - start;

                MessageBox.Show(String.Format("软件有效时间为 {0} 天", Math.Ceiling(span.TotalDays)));
            }

            this.label4.Text = "正在创建授权文件...";

            try
            {
                bool bRet = createAccredit(company, start, end);

                if (bRet)
                {
                    this.label4.Text = "创建授权文件成功!";
                    this.label5.Text = "文件名: " + filename;
                    MessageBox.Show("创建授权文件成功!", "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    this.label4.Text = "创建授权文件失败!";
                    MessageBox.Show("创建授权文件失败!", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                this.label4.Text = "创建授权文件失败!";
                MessageBox.Show("创建授权文件失败! " + ex.Message, "", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private bool createAccredit(String name, DateTime start, DateTime end)
        {
            int ret = 0;
            DateTime baseDate = new DateTime(1970, 1, 1);
            double start_d = (start - baseDate).TotalSeconds;
            double current_d = (start - baseDate).TotalSeconds + 1;
            double end_d = (end - baseDate).TotalSeconds;

            FbConnection.CreateDatabase(GetConnectionString(), true);

            using (FbConnection conn = new FbConnection(GetConnectionString()))
            {
                conn.Open();
                using (FbCommand createTable = conn.CreateCommand())
                {
                    createTable.CommandText = "create table ACCREDIT (ID int, NAME varchar(50) character set UTF8, ST varchar(100) character set UTF8, CU varchar(100) character set UTF8, EN varchar(100) character set UTF8);";
                    ret = createTable.ExecuteNonQuery();
                }

                using (FbCommand insertData = conn.CreateCommand())
                {
                    String start_s = encrypt.EncryptString(start_d.ToString());
                    String current_s = encrypt.EncryptString(current_d.ToString());
                    String end_s = encrypt.EncryptString(end_d.ToString());

                    insertData.CommandText = "insert into ACCREDIT values (@id, @name, @start, @current, @end);";
                    insertData.Parameters.Clear();
                    insertData.Parameters.Add("@id", FbDbType.Integer).Value = 1;
                    insertData.Parameters.Add("@name", FbDbType.VarChar).Value = name;

                    insertData.Parameters.Add("@start", FbDbType.VarChar).Value = start_s;
                    insertData.Parameters.Add("@current", FbDbType.VarChar).Value = current_s;
                    insertData.Parameters.Add("@end", FbDbType.VarChar).Value = end_s;

                    ret = insertData.ExecuteNonQuery();
                }
            }

            return ret > 0;
        }

        private string GetConnectionString()
        {
            FbConnectionStringBuilder cs = new FbConnectionStringBuilder();
            cs.Database = filename;
            cs.UserID = "TG";
            cs.Password = "ADMIN";
            cs.Charset = "UTF8";
            cs.ServerType = FbServerType.Embedded;

            return cs.ToString();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            int ret = verify();

            if (ret == 0)
            {
                MessageBox.Show("OK");
            }
            else if (ret == 404)
            {
                MessageBox.Show("授权文件不存在, 404");
            }
            else if (ret == 501)
            {
                MessageBox.Show("系统时间错误, 501");
            }
            else if (ret == 502)
            {
                MessageBox.Show("授权文件已过期, 502");
            }
            else
            {
                MessageBox.Show("授权文件将于 " + ret + " 天后过期.");
            }
        }

        private int verify()
        {
            double st = 0;
            double cu = 0;
            double en = 0;
            int ret = 0;

            try
            {
                using (FbConnection conn = new FbConnection(GetConnectionString()))
                {
                    conn.Open();
                    using (FbCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "select ID, NAME, ST, CU, EN from ACCREDIT;";
                        using (FbDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                String st_s = reader.GetString(2);
                                String cu_s = reader.GetString(3);
                                String en_s = reader.GetString(4);

                                st = Convert.ToDouble(encrypt.DecryptString(st_s));
                                cu = Convert.ToDouble(encrypt.DecryptString(cu_s));
                                en = Convert.ToDouble(encrypt.DecryptString(en_s));
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ret = 404;
                error = e.Message;
            }

            if (st > 0 && cu > 0 && en > 0)
            {

                if (cu - st <= 0)
                {
                    ret = 501;    // 当前时间错误
                }
                else if ((cu - en) >= 0)
                {
                    ret = 502;    // 过期
                }

                if ((cu - en) < 0)
                {
                    double t = (en - cu);   // second

                    int day = (int)(t / 86400) + 1;
                    ret = day;
                }
            }

            return ret;
        }
    }
}
