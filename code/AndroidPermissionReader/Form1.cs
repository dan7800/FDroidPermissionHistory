using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AndroidPermissionReader
{
    public partial class Form1 : Form
    {
        DataTable sourceTable;
        DataRow row;
        List<FileValues> values;
        List<PermissionHistory> historyList;
        string logFile, sourceFile;

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            button1.Enabled = false;
            button1.Text = "Processing...";
            textBox1.Enabled = false;
            sourceFile = textBox1.Text;

            logFile = string.Format("permission_history_{0}.csv", DateTime.Now.ToFileTime().ToString());
            using (StreamWriter w = File.AppendText(logFile))
            {
                w.WriteLine("AppID;CommitID;PermissionID;AuthorName;AuthorEmail;AlteredDate;AlteredDateTicks;Permission;ActionType;isDangerous;PercentCommitter");
            }

            sourceTable = new DataTable();
            sourceTable.Columns.Add(new DataColumn("AppID", typeof(int)));
            sourceTable.Columns.Add(new DataColumn("CommitID", typeof(int)));
            sourceTable.Columns.Add(new DataColumn("PermissionID", typeof(int)));
            sourceTable.Columns.Add(new DataColumn("AuthorName", typeof(string)));
            sourceTable.Columns.Add(new DataColumn("AuthorEmail", typeof(string)));
            sourceTable.Columns.Add(new DataColumn("AlteredDate", typeof(DateTime)));
            sourceTable.Columns.Add(new DataColumn("AlteredDateTicks", typeof(long)));
            sourceTable.Columns.Add(new DataColumn("Permission", typeof(string)));
            sourceTable.Columns.Add(new DataColumn("ActionType", typeof(string)));
            sourceTable.Columns.Add(new DataColumn("isDangerous", typeof(int)));
            sourceTable.Columns.Add(new DataColumn("PercentCommitter", typeof(double)));

            dataGridView1.DataSource = sourceTable;


            values = File.ReadAllLines(sourceFile)
                .Select(v => FileValues.FromCsv(v))
                .ToList();
                //.Skip(1)               //If the source CSV file has column headers in the first row, then uncomment this line.

            
            processData();


            foreach (var item in historyList)
            {
                row = sourceTable.NewRow();
                row[0] = item.AppID;
                row[1] = item.CommitID;
                row[2] = item.PermissionID;
                row[3] = item.AuthorName;
                row[4] = item.AuthorEmail;
                row[5] = item.AlteredDate;
                row[6] = item.AlteredDate.Ticks;
                row[7] = item.Permission;
                row[8] = item.Action.ToString();
                row[9] = item.isDangerous;
                row[10] = item.PercentCommitter;

                sourceTable.Rows.Add(row);

                using (StreamWriter w = File.AppendText(logFile))
                {
                    w.WriteLine("{0};{1};{2};{3};{4};{5};{6};{7};{8};{9};{10}",
                        item.AppID, item.CommitID, item.PermissionID, item.AuthorName, item.AuthorEmail, item.AlteredDate, item.AlteredDate.Ticks, item.Permission, item.Action.ToString(), item.isDangerous, item.PercentCommitter);
                }
            }

            button1.Enabled = true;
            button1.Text = "Start...";
            textBox1.Enabled = true;
        }

        
        private void processData()
        {
            historyList = new List<PermissionHistory>();
            PermissionHistory historyItem;
            
            //Get unique apps
            var uniqueApps = values.Select(x => new { x.AppID }).Distinct();

            foreach (var app in uniqueApps)
            {
                int currentApp = app.AppID;

                //Get all commits that belong to the current app
                var appCommits = values.Where(a => a.AppID == currentApp);
               
                //Get unique commits for the current app (a single commit can have multiple records)
                var uniqueCommits = appCommits.Select(x => new { x.AlteredDate }).Distinct().OrderBy(z => z.AlteredDate);

                int i = 0;
                List<FileValues> previous = new List<FileValues>();
                List<FileValues> current = new List<FileValues>();
                foreach (var uniqueCommit in uniqueCommits)
                {
                    //Treat the fist commit as an ADD operation
                    if (i == 0)
                    {
                        current = appCommits.Where(x => x.AlteredDate == uniqueCommit.AlteredDate).ToList();
                        foreach(var item in current)
                        {
                            historyItem = new PermissionHistory();
                            historyItem.AppID = item.AppID;
                            historyItem.AlteredDate = item.AlteredDate;
                            historyItem.CommitID = item.CommitID;
                            historyItem.PermissionID = item.PermissionID;
                            historyItem.isDangerous = item.isDangerous;
                            historyItem.AuthorName = item.AuthorName;
                            historyItem.AuthorEmail = item.AuthorEmail;
                            historyItem.CommitMessage = item.CommitMessage;
                            historyItem.Permission = item.Permission;
                            historyItem.PercentCommitter = item.PercentCommitter;
                            historyItem.Action = PermissionHistory.ActionType.ADD;

                            historyList.Add(historyItem);                        
                        }
                        previous = current.ToList();
                    }
                    else
                    {
                        //If the Permission is present in the current commit, but not in the previous commit, then the Permission is has been Added
                        //If the Permission is present in the previous commit, but not in the current commit, then the Permission has been Removed

                        //Get the all commit records of the app that have the same date value. This resultset is the 'current' commit for this iteration
                        current = appCommits.Where(x => x.AlteredDate == uniqueCommit.AlteredDate).ToList();

                        foreach(var item in current)
                        {
                            if (!previous.Exists(f => f.Permission == item.Permission)){
                                historyItem = new PermissionHistory();
                                historyItem.AppID = item.AppID;
                                historyItem.AlteredDate = item.AlteredDate;
                                historyItem.CommitID = item.CommitID;
                                historyItem.PermissionID = item.PermissionID;
                                historyItem.isDangerous = item.isDangerous;
                                historyItem.AuthorName = item.AuthorName;
                                historyItem.AuthorEmail = item.AuthorEmail;
                                historyItem.CommitMessage = item.CommitMessage;
                                historyItem.Permission = item.Permission;
                                historyItem.PercentCommitter = item.PercentCommitter;
                                historyItem.Action = PermissionHistory.ActionType.ADD;

                                historyList.Add(historyItem);
                            }                            
                        }

                        foreach (var item in previous)
                        {
                            if (!current.Exists(f => f.Permission == item.Permission))
                            {
                                historyItem = new PermissionHistory();
                                historyItem.AppID = item.AppID;
                                historyItem.CommitID = current[0].CommitID;
                                historyItem.AlteredDate = current[0].AlteredDate;
                                historyItem.PermissionID = item.PermissionID;
                                historyItem.isDangerous = item.isDangerous;
                                historyItem.AuthorName = current[0].AuthorName;
                                historyItem.AuthorEmail = current[0].AuthorEmail;
                                historyItem.CommitMessage = current[0].CommitMessage;
                                historyItem.Permission = item.Permission;
                                historyItem.PercentCommitter = current[0].PercentCommitter;
                                historyItem.Action = PermissionHistory.ActionType.REMOVE;

                                historyList.Add(historyItem);
                            }

                        }

                        //set the current commit set to 'previous' so it can be used in the next iteration
                        previous = current.ToList();
                    }
            
                    i++;
                }
            }            
        }
    }

    class PermissionHistory
    {
        public int AppID, CommitID, PermissionID, isDangerous;
        public string AuthorName, AuthorEmail, CommitMessage, Permission;
        public DateTime AlteredDate;
        public double PercentCommitter;
        public ActionType Action;

        public enum ActionType
        {
            ADD,REMOVE
        }
    }


    class FileValues
    {
        public int AppID, CommitID, PermissionID, isDangerous;
        public string AuthorName, AuthorEmail, CommitMessage, Permission;
        public DateTime AlteredDate;
        public double PercentCommitter;

        public static FileValues FromCsv(string csvLine)
        {
            //The CSV file is semi-colon deliminated
            string[] values = csvLine.Split(';');
            FileValues dailyValues = new FileValues();

            dailyValues.AppID = Convert.ToInt32(values[0].TrimEnd('\"').TrimStart('\"'));
            dailyValues.CommitID = Convert.ToInt32(values[1].TrimEnd('\"').TrimStart('\"'));
            dailyValues.PermissionID = Convert.ToInt32(values[2].TrimEnd('\"').TrimStart('\"'));
            dailyValues.AuthorName = Convert.ToString(values[3].TrimEnd('\"').TrimStart('\"'));
            dailyValues.AuthorEmail = Convert.ToString(values[4].TrimEnd('\"').TrimStart('\"'));

            DateTime convertedDate = DateTime.ParseExact(values[5].TrimEnd('\"').TrimStart('\"'), "yyyy-M-dd HH:mm:ss",
                                       System.Globalization.CultureInfo.InvariantCulture);

            dailyValues.AlteredDate = convertedDate;
            dailyValues.Permission = Convert.ToString(values[6].TrimEnd('\"').TrimStart('\"'));
            dailyValues.isDangerous = Convert.ToInt32(values[7].TrimEnd('\"').TrimStart('\"'));
            dailyValues.PercentCommitter = Convert.ToDouble(values[8].TrimEnd('\"').TrimStart('\"'));

            return dailyValues;
        }
    }
}
