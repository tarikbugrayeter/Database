using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Database
{
    public partial class Form1 : Form
    {
        private readonly string connectionString = "Server=.\\SQLEXPRESS;Database=AdventureWorks2022;Integrated Security=True;";
        private int deadlockCountA = 0;
        private int deadlockCountB = 0;

        public Form1()
        {
            InitializeComponent();

            // ComboBox'a öğeler ekleyin
            comboBox1.Items.Add("READ UNCOMMITTED");
            comboBox1.Items.Add("READ COMMITTED");
            comboBox1.Items.Add("REPEATABLE READ");
            comboBox1.Items.Add("SERIALIZABLE");

            // Default olarak ilk öğeyi seçin (isteğe bağlı)
            comboBox1.SelectedIndex = 0;

            // Deadlock etiketlerinin başlangıç değeri
            textBox3.Text = "Type A Users Deadlocks: 0";
            textBox4.Text = "Type B Users Deadlocks: 0";
        }

        private void TypeAUserTransaction(string isolationLevel)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Transaction isolation level belirlemek için
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = $"SET TRANSACTION ISOLATION LEVEL {isolationLevel}";
                    command.ExecuteNonQuery();
                }

                // Başlangıç ve bitiş tarihleri
                DateTime beginDate = new DateTime(2011, 01, 01);
                DateTime endDate = new DateTime(2015, 12, 31);

                for (int i = 0; i < 100; i++)
                {
                    try
                    {
                        using (SqlTransaction transaction = connection.BeginTransaction())
                        {
                            // Rastgele tarih seçimi
                            DateTime transactionDate = GetRandomDate(beginDate, endDate);

                            // Update sorgusu için parametre değerleri
                            SqlParameter paramBeginDate = new SqlParameter("@BeginDate", transactionDate);
                            SqlParameter paramEndDate = new SqlParameter("@EndDate", transactionDate.AddYears(1).AddDays(-1));

                            // Update sorgusu
                            string updateQuery = "UPDATE Sales.SalesOrderDetail " +
                                                 "SET UnitPrice = UnitPrice * 10.0 / 10.0 " +
                                                 "WHERE UnitPrice > 100 " +
                                                 "AND EXISTS (SELECT * FROM Sales.SalesOrderHeader " +
                                                             "WHERE Sales.SalesOrderHeader.SalesOrderID = Sales.SalesOrderDetail.SalesOrderID " +
                                                             "AND Sales.SalesOrderHeader.OrderDate BETWEEN @BeginDate AND @EndDate " +
                                                             "AND Sales.SalesOrderHeader.OnlineOrderFlag = 1)";

                            // Update sorgusunu yürütme
                            using (SqlCommand updateCommand = new SqlCommand(updateQuery, connection, transaction))
                            {
                                updateCommand.Parameters.Add(paramBeginDate);
                                updateCommand.Parameters.Add(paramEndDate);
                                updateCommand.ExecuteNonQuery();
                            }

                            // İşlemi gerçekleştirme
                            transaction.Commit();
                        }
                    }
                    catch (SqlException ex)
                    {
                        
                        if (ex.Number == 1205) // Deadlock exception number
                        {
                            lock (this)
                            {
                                deadlockCountA++;
                            }
                        }
                    }
                }
            }
        }

        private void TypeBUserTransaction(string isolationLevel)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Transaction isolation level belirlemek için
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = $"SET TRANSACTION ISOLATION LEVEL {isolationLevel}";
                    command.ExecuteNonQuery();
                }

                // Başlangıç ve bitiş tarihleri
                DateTime beginDate = new DateTime(2011, 01, 01);
                DateTime endDate = new DateTime(2015, 12, 31);

                for (int i = 0; i < 100; i++)
                {
                    try
                    {
                        using (SqlTransaction transaction = connection.BeginTransaction())
                        {
                            // Rastgele tarih seçimi
                            DateTime transactionDate = GetRandomDate(beginDate, endDate);

                            // Select sorgusu için parametre değerleri
                            SqlParameter paramBeginDate = new SqlParameter("@BeginDate", transactionDate);
                            SqlParameter paramEndDate = new SqlParameter("@EndDate", transactionDate.AddYears(1).AddDays(-1));

                            // Select sorgusu
                            string selectQuery = "SELECT SUM(Sales.SalesOrderDetail.OrderQty) " +
                                                 "FROM Sales.SalesOrderDetail " +
                                                 "WHERE UnitPrice > 100 " +
                                                 "AND EXISTS (SELECT * FROM Sales.SalesOrderHeader " +
                                                             "WHERE Sales.SalesOrderHeader.SalesOrderID = Sales.SalesOrderDetail.SalesOrderID " +
                                                             "AND Sales.SalesOrderHeader.OrderDate BETWEEN @BeginDate AND @EndDate " +
                                                             "AND Sales.SalesOrderHeader.OnlineOrderFlag = 1)";
                            
                            var result =new SqlCommand( "SELECT * FROM Sales.SalesOrderDetail" , connection , transaction);
                            using (var reader = result.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    // Her bir satırı işleyin
                                    // Örneğin:
                                    var orderId = reader["SalesOrderID"].ToString();
                                    var productId = reader["ProductID"].ToString();
                                    // vb.
                                }
                            }
                            // Select sorgusunu yürütme
                            using (SqlCommand selectCommand = new SqlCommand(selectQuery, connection, transaction))
                            {
                                selectCommand.Parameters.Add(paramBeginDate);
                                selectCommand.Parameters.Add(paramEndDate);
                                selectCommand.ExecuteScalar();
                            }

                            // İşlemi gerçekleştirme
                            transaction.Commit();
                        }
                    }
                    catch (SqlException ex)
                    {
                        if (ex.Number == 1205) // Deadlock exception number
                        {
                            lock (this)
                            {
                                deadlockCountB++;
                            }
                        }
                    }
                }
            }
        }

        private DateTime GetRandomDate(DateTime startDate, DateTime endDate)
        {
            Random rnd = new Random();
            int range = (endDate - startDate).Days;
            return startDate.AddDays(rnd.Next(range));
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            // Kullanıcı sayısını alın
            int typeAUsers = int.Parse(textBox1.Text);
            int typeBUsers = int.Parse(textBox2.Text);

            // Seçilen izolasyon seviyesini alın
            string selectedIsolationLevel = comboBox1.SelectedItem.ToString();

            Random random = new Random();

            List<Task> tasks = new List<Task>();
             
            // Type A ve Type B kullanıcı işlemlerini başlat
            for (int i = 0; i < typeAUsers + typeBUsers; i++)
            {
                bool isTypeA = random.Next(100) < 50; // %50 olasılıkla Type A
                string isolationLevel = isTypeA ? "READ UNCOMMITTED" : selectedIsolationLevel;

                // Doğru türdeki işlemi başlat
                if (isTypeA)
                {
                    tasks.Add(Task.Run(() => TypeAUserTransaction(isolationLevel)));
                }
                else
                {
                    tasks.Add(Task.Run(() => TypeBUserTransaction(isolationLevel)));
                }
            }

            await Task.WhenAll(tasks);

            // Deadlock sayısını göster
            textBox3.Text = deadlockCountA.ToString();
            textBox4.Text = deadlockCountB.ToString();

            MessageBox.Show("Tüm işlemler tamamlandı.");
        }

        // Ön yüz ile ilgili diğer olaylar
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e) { }

        private void label1_Click(object sender, EventArgs e) { }

        private void textBox2_TextChanged(object sender, EventArgs e) { }

        private void textBox1_TextChanged(object sender, EventArgs e) { }

        private void label2_Click(object sender, EventArgs e) { }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {

        }
        private void textBox4_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
