using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Database
{
    public partial class Form1 : Form
    {
        private readonly string connectionString = "Server=.\\SQLEXPRESS;Database=AdventureWorks2022;Integrated Security=True;";
        private readonly object lockObject = new object();
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

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = $"SET TRANSACTION ISOLATION LEVEL {isolationLevel}";
                    command.ExecuteNonQuery();
                }

                DateTime beginDate = new DateTime(2011, 01, 01);
                DateTime endDate = new DateTime(2015, 12, 31);

                for (int i = 0; i < 100; i++)
                {
                    try
                    {
                        using (SqlTransaction transaction = connection.BeginTransaction())
                        {
                            DateTime transactionDate = GetRandomDate(beginDate, endDate);

                            SqlParameter paramBeginDate = new SqlParameter("@BeginDate", transactionDate);
                            SqlParameter paramEndDate = new SqlParameter("@EndDate", transactionDate.AddYears(1).AddDays(-1));

                            string updateQuery = "UPDATE Sales.SalesOrderDetail " +
                                                 "SET UnitPrice = UnitPrice * 10.0 / 10.0 " +
                                                 "WHERE UnitPrice > 100 " +
                                                 "AND EXISTS (SELECT * FROM Sales.SalesOrderHeader " +
                                                             "WHERE Sales.SalesOrderHeader.SalesOrderID = Sales.SalesOrderDetail.SalesOrderID " +
                                                             "AND Sales.SalesOrderHeader.OrderDate BETWEEN @BeginDate AND @EndDate " +
                                                             "AND Sales.SalesOrderHeader.OnlineOrderFlag = 1)";

                            using (SqlCommand updateCommand = new SqlCommand(updateQuery, connection, transaction))
                            {
                                updateCommand.Parameters.Add(paramBeginDate);
                                updateCommand.Parameters.Add(paramEndDate);
                                updateCommand.CommandTimeout = 120; // Zaman aşımı süresini 120 saniyeye ayarlayın
                                updateCommand.ExecuteNonQuery();
                            }

                            transaction.Commit();
                        }
                    }
                    catch (SqlException ex)
                    {
                        if (ex.Number == 1205) // Deadlock exception number
                        {
                            lock (lockObject)
                            {
                                deadlockCountA++;
                                UpdateTextBox3($"Type A Users Deadlocks: {deadlockCountA}");
                            }
                        }
                        else
                        {
                            MessageBox.Show($"TypeAUserTransaction SQL error: {ex.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"TypeAUserTransaction error: {ex.Message}");
                    }
                }
            }
        }

        private void UpdateTextBox3(string text)
        {
            if (textBox3.InvokeRequired)
            {
                textBox3.Invoke((MethodInvoker)delegate
                {
                    textBox3.Text = text;
                });
            }
            else
            {
                textBox3.Text = text;
            }
        }


        private void TypeBUserTransaction(string isolationLevel)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = $"SET TRANSACTION ISOLATION LEVEL {isolationLevel}";
                    command.ExecuteNonQuery();
                }

                DateTime beginDate = new DateTime(2011, 01, 01);
                DateTime endDate = new DateTime(2015, 12, 31);

                for (int i = 0; i < 100; i++)
                {
                    try
                    {
                        using (SqlTransaction transaction = connection.BeginTransaction())
                        {
                            DateTime transactionDate = GetRandomDate(beginDate, endDate);

                            SqlParameter paramBeginDate = new SqlParameter("@BeginDate", transactionDate);
                            SqlParameter paramEndDate = new SqlParameter("@EndDate", transactionDate.AddYears(1).AddDays(-1));

                            string updateQuery = "UPDATE Sales.SalesOrderDetail " +
                                                 "SET UnitPrice = UnitPrice * 10.0 / 10.0 " +
                                                 "WHERE UnitPrice > 100 " +
                                                 "AND EXISTS (SELECT * FROM Sales.SalesOrderHeader " +
                                                             "WHERE Sales.SalesOrderHeader.SalesOrderID = Sales.SalesOrderDetail.SalesOrderID " +
                                                             "AND Sales.SalesOrderHeader.OrderDate BETWEEN @BeginDate AND @EndDate " +
                                                             "AND Sales.SalesOrderHeader.OnlineOrderFlag = 1)";

                            using (SqlCommand updateCommand = new SqlCommand(updateQuery, connection, transaction))
                            {
                                updateCommand.Parameters.Add(paramBeginDate);
                                updateCommand.Parameters.Add(paramEndDate);
                                updateCommand.CommandTimeout = 120; // Zaman aşımı süresini 120 saniyeye ayarlayın
                                updateCommand.ExecuteNonQuery();
                            }

                            transaction.Commit();
                        }
                    }
                    catch (SqlException ex)
                    {
                        if (ex.Number == 1205) // Deadlock exception number
                        {
                            lock (lockObject)
                            {
                                deadlockCountB++;
                                UpdateTextBox4($"Type B Users Deadlocks: {deadlockCountB}");
                            }
                        }
                        else
                        {
                            MessageBox.Show($"TypeBUserTransaction SQL error: {ex.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"TypeBUserTransaction error: {ex.Message}");
                    }
                }
            }
        }

        private void UpdateTextBox4(string text)
        {
            if (textBox4.InvokeRequired)
            {
                textBox4.Invoke((MethodInvoker)delegate
                {
                    textBox4.Text = text;
                });
            }
            else
            {
                textBox4.Text = text;
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
            // Kullanıcı sayısını ve izolasyon seviyesini alın
            int typeAUsers = int.Parse(textBox1.Text);
            int typeBUsers = int.Parse(textBox2.Text);
            string selectedIsolationLevel = comboBox1.SelectedItem.ToString();

            List<Task> tasks = new List<Task>();
            List<long> typeAUserTimes = new List<long>();
            List<long> typeBUserTimes = new List<long>();

            // Type A ve Type B kullanıcı işlemlerini başlatmak için thread'leri oluşturun
            for (int i = 0; i < typeAUsers; i++)
            {
                Task task = Task.Run(() =>
                {
                    long startTime = Stopwatch.GetTimestamp();
                    TypeAUserTransaction(selectedIsolationLevel);
                    long endTime = Stopwatch.GetTimestamp();
                    typeAUserTimes.Add(endTime - startTime);
                });
                tasks.Add(task);
            }

            for (int i = 0; i < typeBUsers; i++)
            {
                Task task = Task.Run(() =>
                {
                    long startTime = Stopwatch.GetTimestamp();
                    TypeBUserTransaction(selectedIsolationLevel);
                    long endTime = Stopwatch.GetTimestamp();
                    typeBUserTimes.Add(endTime - startTime);
                });
                tasks.Add(task);
            }

            // Tüm thread'lerin tamamlanmasını bekleyin
            await Task.WhenAll(tasks);

            // Ortalama süreleri hesapla
            double averageTimeTypeA = typeAUserTimes.Average();
            double averageTimeTypeB = typeBUserTimes.Average();

            // Deadlock sayısını göster
            textBox3.Text = $"Type A Users Deadlocks: {deadlockCountA}, Average Time: {averageTimeTypeA} ticks";
            textBox4.Text = $"Type B Users Deadlocks: {deadlockCountB}, Average Time: {averageTimeTypeB} ticks";

            MessageBox.Show("Tüm işlemler tamamlandı.");
        }


        // Ön yüz ile ilgili diğer olaylar
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e) { }

        private void label1_Click(object sender, EventArgs e) { }

        private void textBox2_TextChanged(object sender, EventArgs e) { }

        private void textBox1_TextChanged(object sender, EventArgs e) { }

        private void label2_Click(object sender, EventArgs e) { }

        private void textBox3_TextChanged(object sender, EventArgs e) { }

        private void textBox4_TextChanged(object sender, EventArgs e) { }
    }
}
