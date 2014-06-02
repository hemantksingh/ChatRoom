using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel.Syndication;
using System.Threading;
using System.Windows.Forms;
using System.Xml;

namespace ChatRoom
{
    public partial class Form1 : Form
    {
        private string _readToEnd;
        private bool _stop;

        public Form1()
        {
            InitializeComponent();
        }

        private static SyndicationLink
            GetNamedLink(IEnumerable<SyndicationLink> links, string name)
        {
            return links.FirstOrDefault(link => link.RelationshipType == name);
        }

        private static Uri GetLast(Uri head)
        {
            var request = (HttpWebRequest) WebRequest.Create(head);
            request.Credentials = new NetworkCredential("admin", "changeit");
            request.Accept = "application/atom+xml";
            try
            {
                using (var response = (HttpWebResponse) request.GetResponse())
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                        return null;
                    using (XmlReader xmlreader =
                        XmlReader.Create(response.GetResponseStream()))
                    {
                        SyndicationFeed feed = SyndicationFeed.Load(xmlreader);
                        SyndicationLink last = GetNamedLink(feed.Links, "last");
                        return (last != null)
                            ? last.Uri
                            : GetNamedLink(feed.Links, "self").Uri;
                    }
                }
            }
            catch (WebException ex)
            {
                if (((HttpWebResponse) ex.Response).StatusCode ==
                    HttpStatusCode.NotFound) return null;
                throw;
            }
        }

        private void ProcessItem(SyndicationItem item)
        {
            var action = new Action(() => textBox1.Text += (item.Title.Text) + Environment.NewLine);
            BeginInvoke(action);

            //get events
            var request =
                (HttpWebRequest) WebRequest.Create(GetNamedLink(item.Links,
                    "alternate").Uri);
            request.Credentials = new NetworkCredential("admin", "changeit");
            request.Accept = "application/json";
            using (WebResponse response = request.GetResponse())
            {
                var streamReader = new
                    StreamReader(response.GetResponseStream());
                _readToEnd = streamReader.ReadToEnd();
                var write = new Action(() => textBox1.Text += (_readToEnd) + Environment.NewLine);
                BeginInvoke(write);
            }
        }

        private Uri ReadPrevious(Uri uri)
        {
            var request = (HttpWebRequest) WebRequest.Create(uri);
            request.Credentials = new NetworkCredential("admin", "changeit");
            request.Accept = "application/atom+xml";
            using (WebResponse response = request.GetResponse())
            {
                using (XmlReader xmlreader =
                    XmlReader.Create(response.GetResponseStream()))
                {
                    SyndicationFeed feed = SyndicationFeed.Load(xmlreader);
                    foreach (SyndicationItem item in feed.Items.Reverse())
                    {
                        ProcessItem(item);
                    }
                    SyndicationLink prev = GetNamedLink(feed.Links, "previous");
                    return prev == null ? uri : prev.Uri;
                }
            }
        }

        private void PostMessage(string text)
        {
            string message =
                string.Format(
                    "[{{'eventType':'MyFirstEvent', 'eventId' :'" + Guid.NewGuid() +
                    "','data' :{{'name':'{0}','number' :" + new Random().Next() + "}}}}]", text);

            WebRequest request =
                WebRequest.Create("http://127.0.0.1:2113/streams/chat-GeneralChat");
            request.Method = "POST";
            request.ContentType = "application/json";
            request.ContentLength = message.Length;
            using (var sw = new StreamWriter(request.GetRequestStream()))
            {
                sw.Write(message);
            }
            using (WebResponse response = request.GetResponse())
            {
                response.Close();
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            _stop = true;
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            //var timer = new Timer(o => PostMessage(), null, 1000, 1000);

            var backgroundWorker = new BackgroundWorker();

            backgroundWorker.DoWork += (o, args) =>
            {
                Uri last = null;
                while (last == null && !_stop)
                {
                    last = GetLast(new
                        Uri("http://127.0.01:2113/streams/chat-GeneralChat"));
                    if (last == null) Thread.Sleep(1000);
                }

                while (!_stop)
                {
                    Uri current = ReadPrevious(last);
                    if (last == current)
                    {
                        Thread.Sleep(1000);
                    }
                    last = current;
                }
            };

            backgroundWorker.RunWorkerAsync();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            PostMessage(textBox2.Text);
            textBox2.Text = string.Empty;
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            textBox1.SelectionStart += textBox1.Text.Length;
        }
    }
}