using Grpc.Core;
using Grpc.Net.Client;
using Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Client
{
    public partial class Form1 : Form
    {
        static string target = "https://localhost:5001";
        static GrpcChannel channel = GrpcChannel.ForAddress(target);
        static CarDealing.CarDealingClient client = new CarDealing.CarDealingClient(channel);

        Session_Id guid = new Session_Id();
        static List<string> cars = new List<string>();
        public Form1()
        {
            InitializeComponent();
            button2.Hide();
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            listBox1.Items.Clear();
            textBox3.Text = "";
            textBox4.Text = "";
            textBox5.Text = "";
            textBox6.Text = "";
            label17.Text = "";
            label18.Text = "";
            label19.Text = "";
            label20.Text = "";
            label21.Text = "";

            var response = client.ListCars(new ListCarsRequest() { Uid = guid.Id });

            while (await response.ResponseStream.MoveNext())
            {
                listBox1.Items.Add(response.ResponseStream.Current.Car.Numberplate.ToString());

            }

            var balance = client.Balance(new BalanceRequest() { Uid = guid.Id });
            label6.Text = string.Format("Egyenleg: {0:#,#} Ft", balance.Balance);
        }

        //Kijelentkezés gomb
        private void button2_Click(object sender, EventArgs e)
        {
            var res = client.Logout(guid);
            label1.Text = "";
            textBox1.Show();
            textBox2.Show();
            button1.Show();
            label3.Show();
            label4.Show();
            button2.Hide();
            Form1_Load(sender, e);
            MessageBox.Show(res.Success, "", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        //Bejelentkezés gomb
        private void button1_Click(object sender, EventArgs e)
        {
            User user = new User()
            {
                Username = textBox1.Text,
                Password = textBox2.Text
            };
            guid = client.Login(user);

            if (String.IsNullOrWhiteSpace(textBox1.Text) || String.IsNullOrWhiteSpace(textBox2.Text))
            {
                MessageBox.Show(guid.Message, "Sikertelen bejelentkezés", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                if (guid.Id == "")
                    MessageBox.Show(guid.Message, "Sikertelen bejelentkezés", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                else
                {
                    label1.Text = "Bejelentkezve: " + textBox1.Text;
                    textBox1.Hide();
                    textBox2.Hide();
                    label3.Hide();
                    label4.Hide();
                    button1.Hide();
                    button2.Show();
                    MessageBox.Show(guid.Message, "Üdv " + textBox1.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Form1_Load(sender, e);
                }
            }

        }

        //Listboxban levő rendszámokra kattintás
        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBox1.SelectedItem != null)
            {
                var response = client.ActualCar(new ActualCarRequest() { Numberplate = listBox1.SelectedItem.ToString() });
                label17.Text = response.Car.Numberplate;
                label18.Text = response.Car.Brand;
                label19.Text = response.Car.Vintage.ToString();
                label20.Text = string.Format("{0:#,#} Ft", response.Car.Boughtprice);
                label21.Text = string.Format("{0:#,#} Ft", response.Car.Currentvalue);
            }
        }

        //Autó megvétele gomb
        private void button3_Click(object sender, EventArgs e)
        {
            int a;
            int b;

            var response = client.PurchaseCar(new PurchaseCarRequest()

            {
                Uid = guid.Id,

                Car = new Car()
                {
                    Numberplate = textBox3.Text,
                    Brand = textBox4.Text,
                    Vintage = int.TryParse(textBox5.Text, out a) ? a : 0,
                    Boughtprice = int.TryParse(textBox6.Text, out b) ? b : 0,
                    Currentvalue = b

                }
            });

            if (guid.Id == "")
                MessageBox.Show(response.Message, "Sikertelen autóvásárlás!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else
            {
                if (cars.Contains(textBox3.Text))
                    MessageBox.Show(response.Message, "Sikertelen autóvásárlás!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                else
                {
                    if (textBox3.Text == "" || textBox4.Text == "" || a <= 0 || b <= 0)
                        MessageBox.Show(response.Message, "Sikertelen autóvásárlás!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    else
                    {
                        MessageBox.Show(response.Car.Numberplate + " megvéve.", response.Message, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        Form1_Load(sender, e);
                        cars.Add(response.Car.Numberplate);
                    }

                }
            }
        }

        //Autó eladása gomb
        private void button5_Click(object sender, EventArgs e)
        {
            var response = client.SellCar(new SellCarRequest() { Numberplate = listBox1.SelectedItem == null ? "" : listBox1.SelectedItem.ToString() });

            if (listBox1.SelectedItem == null)
                MessageBox.Show(response.Message, "Sikertelen autóeladás!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else
            {
                MessageBox.Show(response.Message, "Sikeres autóeladás!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                cars.Remove(listBox1.SelectedItem.ToString());
                Form1_Load(sender, e);

            }
        }

        //Autó javítása gomb
        private void button4_Click(object sender, EventArgs e)
        {
            var response = client.RepairCar(new RepairCarRequest() { Numberplate = listBox1.SelectedItem == null ? "" : listBox1.SelectedItem.ToString() });

            if (listBox1.SelectedItem == null)
                MessageBox.Show(response.Message, "Sikertelen autójavítás!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else
            {
                MessageBox.Show(response.Message, "Sikeres autójavítás!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Form1_Load(sender, e);

            }
        }

        private void label8_Click(object sender, EventArgs e)
        {

        }
    }
}
