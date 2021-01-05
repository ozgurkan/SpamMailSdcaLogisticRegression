using JR.Utils.GUI.Forms;
using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Exchange.WebServices.Data;
using AE.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.ML.Trainers;

namespace SpamMailSdcaLogisticRegression
{
    public partial class Form1 : Form
    {
        
        //static readonly string _path = "..\\..\\..\\Data\\ham-spam.csv";// Verinin alınacağı yol
        static readonly string _path = "C:\\Program Files\\OzgurKAN\\SpamMailSetup\\Data\\ham-spam.csv";
        dynamic predictor; // Model tahmini için kullanılan dinamik değişken.       
        static ImapClient IC; //Gmail bağlantısı için Imap Server değişkeni
        ExchangeService exchange = null; // Outlook bağlantısı için exchange değişkeni
        string[] basliklar = new string[500];//Maillerin başlıklarını tutmak için 500 elemanlı dizi
        string[] icerikler = new string[500];//Maillerin içeriklerini tutmak için 500 elemanlı dizi
        int i = 0;//Maillerde gezinmeyi sağlayan sayaç
        string username;//serverlara bağlantı için kullanılan kullanıcı adı
        string domain;//serverlara bağlantı için kulllanılan alan adı

        public Form1()
        {
            InitializeComponent();
            lstMsg.Clear();
            lstMsg.View = View.Details;
            lstMsg.Columns.Add("Tarih/Saat", 170);
            lstMsg.Columns.Add("Gönderen", 250);
            lstMsg.Columns.Add("Konu",210);
            lstMsg.Columns.Add("İçerik", 400);
            lstMsg.Columns.Add("Spam", 60);
            lstMsg.Columns.Add("Rate", 70);
            lstMsg.FullRowSelect = true;
            lstMsg.ShowItemToolTips = true;
        }

        private void Form1_Shown(object sender, EventArgs e)
        {           
            EgitimYap();
            //SonucDon(predictor);
            button2.Visible = true;
            button1.Visible = true;
            button4.Visible = true;
        }
        
        public void EgitimYap()
        {
            label22.Text = "Model eğitiliyor...";
            var context = new MLContext(seed: 0);

            // Load the data
            var data = context.Data.LoadFromTextFile<Input>(_path, hasHeader: true, separatorChar: ',');

            // Split the data into a training set and a test set
            var trainTestData = context.Data.TrainTestSplit(data, testFraction: 0.2, seed: 0);
            var trainData = trainTestData.TrainSet;
            var testData = trainTestData.TestSet;


            // Eğitim için gerekli gördüğümüz özellikleri tanımlıyoruz.
            var options = new SdcaLogisticRegressionBinaryTrainer.Options()
            {
                // Yakınsama toleransını ayarlar.
                ConvergenceTolerance = 0.05f,
                // Eğitim verileri üzerinden maksimum iterasyon sayısını belirler.
                MaximumNumberOfIterations = 1000,
                // Pozitif sınıfın örneklerine biraz daha fazla ağırlık verir.
                //PositiveInstanceWeight = 1.2f,
            };

            // Build and train the model
            var pipeline = context.Transforms.Text.FeaturizeText(outputColumnName: "Features", inputColumnName: "Text")
                .Append(context.BinaryClassification.Trainers.SdcaLogisticRegression(options));            
            
            var model = pipeline.Fit(trainData);

            // Evaluate the model
            var predictions = model.Transform(testData);
            var metrics = context.BinaryClassification.Evaluate(predictions, "Label");

            var TP = metrics.ConfusionMatrix.Counts[0][0];            
            var FP = metrics.ConfusionMatrix.Counts[1][0];
            var FN = metrics.ConfusionMatrix.Counts[0][1];
            var TN = metrics.ConfusionMatrix.Counts[1][1];


            var Prevalence = (TP + FN) / (TP + FP + FN + TN);
            var Accuracy = (TP + TN) / (TP + FP + FN + TN);
            var Auc = metrics.AreaUnderPrecisionRecallCurve;


            var Ppv = TP / (TP + FP); // Positive predictive value (PPV), Precision 
            var Fdr = FP / (TP + FP); // False discovery rate (FDR) 
            var For = FN / (FN + TN); // False omission rate (FOR) 
            var Npv = TN / (FN + TN); // Negative predictive value (NPV) 

            var Tpr = TP / (TP + FN); // True positive rate (TPR), Recall, Sensitivity, probability of detection, Power
            var Fpr = FP / (FP + TN); // False positive rate (FPR), Fall-out, probability of false alarm (1-Specificity)
            var Fnr = FN / (TP + FN); // False negative rate (FNR), Miss rate
            var Tnr = TN / (FP + TN); // True negative rate (TNR), Specificity (SPC), Selectivity

            var LrArti = (Tpr) / (Fpr); // Positive likelihood ratio (LR+)
            var LrEksi = (Fnr) / (Tnr); // Negative likelihood ratio (LR−)
            var Dor = (LrArti) / (LrEksi); // Diagnostic odds ratio (DOR)
            var F1 = 2 * ((Ppv * Tpr) / (Ppv + Tpr)); // F1 score

            /*var PositivePrecision = metrics.PositivePrecision;
            var NegativePrecision = metrics.NegativePrecision;
            var PositiveRecall = metrics.PositiveRecall;
            var NegativeRecall = metrics.NegativeRecall;*/

            CreateConfusionMatrix(TN,FP,FN,TP);
            CreateResults(Prevalence, Accuracy, Auc, Ppv, Fdr, For, Npv, Tpr, Fpr, Fnr, Tnr, LrArti, LrEksi, Dor, F1);

           // Use the model to make predictions
            predictor = context.Model.CreatePredictionEngine<Input, Output>(model);
            label22.Text = "Model eğitimi tamamlandı.";           
        }

        public void CreateConfusionMatrix(dynamic TN,dynamic FP,dynamic FN,dynamic TP)
        {
            DataTable table = new DataTable(); //tablomuzu oluşturduk..
            table.Columns.Add("Actual\nPredicted"); //table isimli tabloya ilk kolonumuzu ekledik…
            table.Columns.Add("Actual\nTrue"); //table isimli tabloya ikinci kolonumuzu ekledik…
            table.Columns.Add("Actual\nFalse"); //table isimli tabloya üçüncü kolonumuzu ekledik…

            DataRow row = table.NewRow(); //Tablo için satır oluşturduk..
            row["Actual\nPredicted"] = "Predicted True"; //1. satır Adı kolonu değeri..
            row["Actual\nTrue"] = TP; //1. satır Adı kolonu değeri..
            row["Actual\nFalse"] = FP; //1. satır Soyadı alanı değeri..
            table.Rows.Add(row); //Satırı tabloya ekledik. Bu işlemi yapmazsak ne kadar satır oluşturursak oluşturalım tablomuzda görünmez ! ..


            DataRow row2 = table.NewRow(); //Tablo için satır oluşturduk..
            row2["Actual\nPredicted"] = "Predicted False"; //1. satır Adı kolonu değeri..
            row2["Actual\nTrue"] = FN; //1. satır Adı kolonu değeri..
            row2["Actual\nFalse"] = TN; //1. satır Soyadı alanı değeri..
            table.Rows.Add(row2); //Satırı tabloya ekledik. Bu işlemi yapmazsak ne kadar satır oluşturursak oluşturalım tablomuzda görünmez ! ..
  
            dataGridView1.DataSource = table; //Tablomuzu görebilmek için gridControl’e yükledik..            
            dataGridView1.Columns[1].DefaultCellStyle.Font = new Font("Verdana", 14, FontStyle.Bold);
            dataGridView1.Columns[2].DefaultCellStyle.Font = new Font("Verdana", 14, FontStyle.Bold);
            dataGridView1.Columns[0].Width = 120;
            dataGridView1.Columns[1].Width = 120;
            dataGridView1.Columns[2].Width = 120;

            DataGridViewCellStyle style = new DataGridViewCellStyle();            
            style.ForeColor = Color.Red;
            dataGridView1.Rows[0].Cells[2].Style = style;
            dataGridView1.Rows[1].Cells[1].Style = style;

            DataGridViewCellStyle style1 = new DataGridViewCellStyle();
            style1.ForeColor = Color.Green;
            dataGridView1.Rows[0].Cells[1].Style = style1;
            dataGridView1.Rows[1].Cells[2].Style = style1;

        }

        public void CreateResults(dynamic Prevalence,dynamic Accuracy,dynamic Auc,dynamic Ppv,dynamic Fdr,dynamic For, dynamic Npv,dynamic Tpr, dynamic Fpr,dynamic Fnr,dynamic Tnr,dynamic LrArti,dynamic LrEksi,dynamic Dor,dynamic F1)
        {
            DataTable table = new DataTable();
            table.Columns.Add("PREVALENCE"); 
            table.Columns.Add("ACCURACY"); 
            table.Columns.Add("AUC"); 
            table.Columns.Add("PPV"); 
            table.Columns.Add("FDR"); 
            table.Columns.Add("FOR"); 
            table.Columns.Add("NPV");
            table.Columns.Add("TPR");
            table.Columns.Add("FPR");
            table.Columns.Add("FNR");
            table.Columns.Add("TNR");

            DataRow row = table.NewRow(); 
            row["PREVALENCE"] = Prevalence.ToString("N2"); 
            row["ACCURACY"] = Accuracy.ToString("N2"); 
            row["AUC"] = Auc.ToString("N2");
            row["PPV"] = Ppv.ToString("N2"); 
            row["FDR"] = Fdr.ToString("N2"); 
            row["FOR"] = For.ToString("N2"); 
            row["NPV"] = Npv.ToString("N2");
            row["TPR"] = Tpr.ToString("N2");
            row["FPR"] = Fpr.ToString("N2");
            row["FNR"] = Fnr.ToString("N2");
            row["TNR"] = Tnr.ToString("N2");
            table.Rows.Add(row);            

            dataGridView2.DataSource = table; //Tablomuzu görebilmek için gridControl’e yükledik.. 
            dataGridView2.Columns[0].DefaultCellStyle.Font = new Font("Verdana", 14, FontStyle.Bold);
            dataGridView2.Columns[1].DefaultCellStyle.Font = new Font("Verdana", 14, FontStyle.Bold);
            dataGridView2.Columns[2].DefaultCellStyle.Font = new Font("Verdana", 14, FontStyle.Bold);
            dataGridView2.Columns[3].DefaultCellStyle.Font = new Font("Verdana", 14, FontStyle.Bold);
            dataGridView2.Columns[4].DefaultCellStyle.Font = new Font("Verdana", 14, FontStyle.Bold);
            dataGridView2.Columns[5].DefaultCellStyle.Font = new Font("Verdana", 14, FontStyle.Bold);
            dataGridView2.Columns[6].DefaultCellStyle.Font = new Font("Verdana", 14, FontStyle.Bold);
            dataGridView2.Columns[7].DefaultCellStyle.Font = new Font("Verdana", 14, FontStyle.Bold);
            dataGridView2.Columns[8].DefaultCellStyle.Font = new Font("Verdana", 14, FontStyle.Bold);
            dataGridView2.Columns[9].DefaultCellStyle.Font = new Font("Verdana", 14, FontStyle.Bold);
            dataGridView2.Columns[10].DefaultCellStyle.Font = new Font("Verdana", 14, FontStyle.Bold);




            DataTable table1 = new DataTable();
            table1.Columns.Add("LR+");
            table1.Columns.Add("LR-");
            table1.Columns.Add("DOR");
            table1.Columns.Add("F1");

            DataRow row1 = table1.NewRow();
            row1["LR+"] = LrArti.ToString("N2"); 
            row1["LR-"] = LrEksi.ToString("N2"); 
            row1["DOR"] = Dor.ToString("N2"); 
            row1["F1"] = F1.ToString("N2");
            table1.Rows.Add(row1);

            dataGridView3.DataSource = table1; //Tablomuzu görebilmek için gridControl’e yükledik.. 
            dataGridView3.Columns[0].DefaultCellStyle.Font = new Font("Verdana", 14, FontStyle.Bold);
            dataGridView3.Columns[1].DefaultCellStyle.Font = new Font("Verdana", 14, FontStyle.Bold);
            dataGridView3.Columns[2].DefaultCellStyle.Font = new Font("Verdana", 14, FontStyle.Bold);
            dataGridView3.Columns[3].DefaultCellStyle.Font = new Font("Verdana", 14, FontStyle.Bold);

            dataGridView3.Columns[0].Width = 120;
            dataGridView3.Columns[1].Width = 120;
            dataGridView3.Columns[2].Width = 120;
            dataGridView3.Columns[3].Width = 120;
        }

        public void SonucDon(dynamic predictor)
        {            
            var input = new Input { Text = textBox1.Text };
            var prediction = predictor.Predict(input);            
            FlexibleMessageBox.FONT= new Font("Verdana", 14, FontStyle.Bold);
            FlexibleMessageBox.Show("Girilen Metin: "+ (Convert.ToBoolean(prediction.Prediction) ? "Spam" : "Not spam")+"\n"+"Spam Skoru: "+ prediction.Probability, "Sonuç Mesajı");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Size = new Size(1111, 449);
            panel2.Visible = true;
            panel1.Visible = false;
            panel3.Visible = false;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            panel1.Visible = false;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Size = new Size(1460,880);
            panel1.Visible = true;
            panel2.Visible = false;
            panel3.Visible = false;
        }

        private void button5_Click(object sender, EventArgs e)
        {
            textBox1.Text = "";
            panel2.Visible = false;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            this.Size = new Size(1454, 664);
            panel3.Visible = true;
            panel1.Visible = false;
            panel2.Visible = false;
        }

        private void button6_Click(object sender, EventArgs e)
        {
            panel3.Visible = false;
        }

        private void button7_Click(object sender, EventArgs e)
        {
            if (textBox1.Text=="")
            {
                FlexibleMessageBox.FONT= new Font("Verdana", 14, FontStyle.Bold);
                FlexibleMessageBox.Show("Lütfen bir metin giriniz.");
            }
            else
            {
                SonucDon(predictor);
            }
        }

        public void ConnectToExchangeServer()
        {
            lblMsg.Text = "Exchange Server'a bağlanılıyor....";
            lblMsg.Refresh();
            try
            {
                exchange = new ExchangeService(ExchangeVersion.Exchange2007_SP1);
                exchange.Credentials = new WebCredentials(textBox3.Text, textBox2.Text, domain);
                exchange.AutodiscoverUrl(textBox3.Text, RedirectionCallback);

                lblMsg.Text = "Exchange Server'a bağlandı : " + exchange.Url.Host + "\n Günlük Mailler Gösteriliyor.";
                lblMsg.Refresh();

            }
            catch (Exception ex)
            {
                lblMsg.Text = "Exchange Server'a bağlanırken hata oluştu.Lütfen maili ve şifreyi kontrol edin.\n" + ex.Message;
                lblMsg.Refresh();
            }

        }

        static bool RedirectionCallback(string url)
        {
            bool redirectionValidated = false;
            Uri redirectionUri = new Uri(url);

            //There are two ways of implementing a RedirectionCallback scheme

            // Way 1: Return true if the URL is an HTTPS URL.
            //return url.ToLower().StartsWith("https://");
            if (redirectionUri.Scheme == "https")
                redirectionValidated = true;

            //Way 2: check if url is autodiscovery url
            if (url.Equals(
                "https://autodiscover-s.outlook.com/autodiscover/autodiscover.xml"))
                redirectionValidated = true;

            return redirectionValidated;
        }

        public static void colorListcolor(ListView lsvMain)
        {
            foreach (ListViewItem lvw in lsvMain.Items)
            {
                lvw.UseItemStyleForSubItems = false;

                for (int i = 0; i < lsvMain.Columns.Count; i++)
                {
                    if (lvw.SubItems[4].Text.ToString() == "YES")
                    {
                        lvw.SubItems[4].BackColor = Color.Red;
                        lvw.SubItems[4].ForeColor = Color.White;
                    }
                    else
                    {
                        lvw.SubItems[4].BackColor = Color.Green;
                        lvw.SubItems[4].ForeColor = Color.White;
                    }
                }
            }
        }

        public static dynamic Cast(dynamic obj, Type castTo)
        {
            return Convert.ChangeType(obj, castTo);
        }
       
        private void button8_Click(object sender, EventArgs e)
        {
            lstMsg.Visible = false;            
            if (textBox3.Text == "" || textBox2.Text == "")
            {
                MessageBox.Show("Lütfen mail adresi ve şifrenizi giriniz.");
            }
            else
            {
                if (Regex.IsMatch(textBox3.Text, @"(@)"))
                {
                    this.Size = new Size(1454, 664);
                    this.Location = new Point(50, 50);
                    username = textBox3.Text.Split('@')[0];
                    domain = textBox3.Text.Split('@')[1];

                    lblMsg.Visible = true;
                    i = 0;
                    lstMsg.Items.Clear();
                    if (domain == "hotmail.com" || domain == "std.yildiz.edu.tr")
                    {
                        ConnectToExchangeServer();
                        TimeSpan ts = new TimeSpan(0, -24, 0, 0);
                        DateTime date = DateTime.Now.Add(ts);
                        SearchFilter.IsGreaterThanOrEqualTo filter = new SearchFilter.IsGreaterThanOrEqualTo(ItemSchema.DateTimeReceived, date);

                        if (exchange != null)
                        {
                            PropertySet itempropertyset = new PropertySet(BasePropertySet.FirstClassProperties);
                            itempropertyset.RequestedBodyType = BodyType.Text;
                            ItemView itemview = new ItemView(1000);
                            itemview.PropertySet = itempropertyset;

                            //FindItemsResults<Item> findResults = service.FindItems(WellKnownFolderName.Inbox, "subject:TODO", itemview);
                            
                            lstMsg.Width = 1170;
                            lstMsg.Height = 450;
                            button8.Text = "Yenile";
                            try
                            {
                                FindItemsResults<Item> findResults = exchange.FindItems(WellKnownFolderName.Inbox, filter, new ItemView(100));
                                foreach (Item item in findResults)
                                {
                                    lstMsg.Visible = true;
                                    item.Load(itempropertyset);
                                    String content = item.Body;
                                    icerikler[i] = content;


                                    var input = new Input { Text = content };
                                    var prediction = predictor.Predict(input);

                                    
                                    double skor = Cast(prediction.Probability, typeof(double));

                                    String durum;
                                    if ((Convert.ToBoolean(prediction.Prediction) ? "Spam" : "Not spam") == "Spam")
                                    {
                                        durum = "YES";
                                    }
                                    else
                                    {
                                        durum = "NO";
                                    }

                                    EmailMessage message = EmailMessage.Bind(exchange, item.Id);
                                    basliklar[i] = message.Subject;
                                    i++;
                                    ListViewItem listitem = new ListViewItem(new[]
                                    {
                                         message.DateTimeReceived.ToString(), message.From.Name.ToString() + "(" + message.From.Address.ToString() + ")", message.Subject,
                                         content,durum,skor.ToString("N2")
                                     });

                                    lstMsg.Items.Add(listitem);

                                }
                                if (findResults.Items.Count <= 0)
                                {
                                    lstMsg.Items.Add("Yeni Mail Bulunamadı.!!");

                                }
                                colorListcolor(lstMsg);
                            }
                            catch
                            {
                                MessageBox.Show("Mail adresi veya şifre yanlış.");
                                textBox3.Text = "";
                                textBox2.Text = "";
                                lblMsg.Text = "";
                                lblMsg.Visible = false;                                
                                button8.Text = "Giriş";
                                Screen screen = Screen.FromControl(this);

                                Rectangle workingArea = screen.WorkingArea;
                                this.Location = new Point()
                                {
                                    X = Math.Max(workingArea.X, workingArea.X + (workingArea.Width - this.Width) / 2),
                                    Y = Math.Max(workingArea.Y, workingArea.Y + (workingArea.Height - this.Height) / 2)
                                };
                            }
                        }
                    }
                    else if (domain == "gmail.com")
                    {
                        lblMsg.Text = "IMAP Server'a bağlanılıyor....";
                        lblMsg.Refresh();
                        try
                        {
                            IC = new ImapClient("imap.gmail.com", textBox3.Text, textBox2.Text, AuthMethods.Login, 993, true);
                            lblMsg.Text = "IMAP Server'a bağlandı.\nGünlük Mailler Gösteriliyor.";
                            lblMsg.Refresh();
                            IC.SelectMailbox("INBOX");
                            int mailCount = IC.GetMessageCount();
                            mailCount--;
                            var Email = IC.GetMessage(mailCount);
                            DateTime localDate = DateTime.Now;
                            TimeSpan baseInterval = new TimeSpan(24, 0, 0);
                            var value = localDate.Subtract(Email.Date);
                            
                            lstMsg.Visible = true;
                            lstMsg.Width = 1170;
                            lstMsg.Height = 450;
                            button8.Text = "Yenile";

                            while (TimeSpan.Compare(baseInterval, value) == 1)
                            {
                                basliklar[i] = Email.Subject.ToString();
                                icerikler[i] = Email.Body.ToString();
                                i++;


                                var input = new Input { Text = Email.Body };
                                var prediction = predictor.Predict(input);

                                double skor = Cast(prediction.Probability, typeof(double));

                                String durum;
                                if ((Convert.ToBoolean(prediction.Prediction) ? "Spam" : "Not spam") == "Spam")
                                {
                                    durum = "YES";
                                }
                                else
                                {
                                    durum = "NO";
                                }
                                var content = Email.Body.ToString();
                                ListViewItem listitem = new ListViewItem(new[]
                                    {
                                         Email.Date.ToString(), Email.From.Address.ToString(), Email.Subject.ToString(),
                                         content,durum,skor.ToString("N2")
                                     });

                                lstMsg.Items.Add(listitem);
                                //MessageBox.Show(Email.Subject.ToString());
                                mailCount--;
                                Email = IC.GetMessage(mailCount);
                                value = localDate.Subtract(Email.Date);
                            }
                            colorListcolor(lstMsg);
                        }
                        catch
                        {
                            MessageBox.Show("Lütfen email güvenlik ayarlarından Daha az güvenli uygulamalara izin ver: AÇIK yapınız.");
                        }
                    }
                    else
                    {
                        MessageBox.Show("Geçersiz bir domain ismi girdiniz.Lütfen kontrol edin!");
                    }

                }
                else
                {
                    MessageBox.Show("Lütfen doğru bir mail formatı giriniz.");
                }
            }
        }

        private void lstMsg_SelectedIndexChanged(object sender, EventArgs e)
        {
            ListView.SelectedIndexCollection indices = lstMsg.SelectedIndices;
            if (indices.Count > 0)
            {
                FlexibleMessageBox.Show(icerikler[indices[0]], basliklar[indices[0]]);
            }
        }
    }

    //Modelin girdi kısmı için oluşturulan sınıf
    public class Input
    {
        [LoadColumn(0), ColumnName("Label")]
        public bool IsSpam;

        [LoadColumn(1)]
        public string Text;
    }

    //Modelin çıktı kısmı için oluşturulan ınıf
    public class Output
    {
        [ColumnName("PredictedLabel")]
        public bool Prediction { get; set; }
        public float Probability { get; set; }
    }
}
