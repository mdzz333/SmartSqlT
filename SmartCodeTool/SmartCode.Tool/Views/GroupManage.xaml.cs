﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using HandyControl.Controls;
using HandyControl.Data;
using SmartCode.Framework;
using SmartCode.Framework.Exporter;
using SmartCode.Framework.PhysicalDataModel;
using SmartCode.Framework.SqliteModel;
using SmartCode.Tool.Annotations;

namespace SmartCode.Tool.Views
{
    //定义委托
    public delegate void ChangeRefreshHandler();
    /// <summary>
    /// GroupManage.xaml 的交互逻辑
    /// </summary>
    public partial class GroupManage : INotifyPropertyChanged
    {
        private string _defaultDatabase = ConfigurationManager.AppSettings["defaultDatabase"];
        public event ChangeRefreshHandler ChangeRefreshEvent;
        public GroupManage()
        {
            InitializeComponent();
            DataContext = this;
        }

        #region DependencyProperty
        public static readonly DependencyProperty ConnectionProperty = DependencyProperty.Register(
            "Connection", typeof(DataBasesConfig), typeof(GroupManage), new PropertyMetadata(default(DataBasesConfig)));
        public DataBasesConfig Connection
        {
            get => (DataBasesConfig)GetValue(ConnectionProperty);
            set => SetValue(ConnectionProperty, value);
        }

        public static readonly DependencyProperty SelectedDataBaseProperty = DependencyProperty.Register(
            "SelectedDataBase", typeof(string), typeof(GroupManage), new PropertyMetadata(default(string)));
        public string SelectedDataBase
        {
            get => (string)GetValue(SelectedDataBaseProperty);
            set => SetValue(SelectedDataBaseProperty, value);
        }

        public new static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            "Title", typeof(string), typeof(GroupManage), new PropertyMetadata(default(string)));
        public new string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty DataListProperty = DependencyProperty.Register(
            "DataList", typeof(List<ObjectGroup>), typeof(GroupManage), new PropertyMetadata(default(List<ObjectGroup>)));
        public List<ObjectGroup> DataList
        {
            get => (List<ObjectGroup>)GetValue(DataListProperty);
            set
            {
                SetValue(DataListProperty, value);
                OnPropertyChanged("DataList");
            }
        }
        #endregion

        private void GroupManage_OnLoaded(object sender, RoutedEventArgs e)
        {
            Title = $"{Connection.DbName} - 分组管理";
            var conn = Connection;
            var selectDataBase = SelectedDataBase;
            var dbConnectionString = conn.DbConnectString.Replace(selectDataBase, "master");
            Task.Run(() =>
            {
                var sqLiteHelper = new SQLiteHelper();
                var datalist = sqLiteHelper.db.Table<ObjectGroup>().
                    Where(x => x.ConnectionName == conn.DbName && x.DataBaseName == selectDataBase).OrderBy(x => x.OrderFlag).ToList();
                Dispatcher.Invoke(() =>
                {
                    DataList = datalist;
                });
                try
                {
                    IExporter exporter = new SqlServer2008Exporter();
                    var lsit = exporter.GetDatabases(dbConnectionString);
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var DBase = lsit;
                        SelectDatabase.ItemsSource = DBase;
                        SelectDatabase.SelectedItem = lsit.FirstOrDefault(x => x.DbName == SelectedDataBase);

                    }));
                }
                catch (Exception ex)
                {

                }
            });
        }

        private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = (ListBox)sender;
            if (listBox.SelectedItems.Count > 0)
            {
                var group = (ObjectGroup)listBox.SelectedItems[0];

                HidId.Text = group.Id.ToString();
                TextGourpName.Text = group.GroupName;
                BtnDelete.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// 保存
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnSave_OnClick(object sender, RoutedEventArgs e)
        {
            var groupName = TextGourpName.Text.Trim();
            if (string.IsNullOrEmpty(groupName))
            {
                Growl.Warning(new GrowlInfo { Message = $"请填写分组名", WaitTime = 1, ShowDateTime = false });
                return;
            }
            var selectedDatabase = (DataBase)SelectDatabase.SelectedItem;
            var sqLiteHelper = new SQLiteHelper();
            var groupId = Convert.ToInt32(HidId.Text);
            if (groupId > 0)
            {
                var groupO = sqLiteHelper.db.Table<ObjectGroup>().FirstOrDefault(x => x.ConnectionName == Connection.DbName && x.DataBaseName == selectedDatabase.DbName && x.Id != groupId && x.GroupName == groupName);
                if (groupO != null)
                {
                    Growl.Warning(new GrowlInfo { Message = $"已存在相同名称的分组名", WaitTime = 1, ShowDateTime = false });
                    return;
                }
                var selectedGroup = (ObjectGroup)ListGroup.SelectedItems[0];
                selectedGroup.GroupName = groupName;
                sqLiteHelper.db.Update(selectedGroup);
            }
            else
            {
                var groupO = sqLiteHelper.db.Table<ObjectGroup>().FirstOrDefault(x => x.ConnectionName == Connection.DbName && x.DataBaseName == selectedDatabase.DbName && x.GroupName == groupName);
                if (groupO != null)
                {
                    Growl.Warning(new GrowlInfo { Message = $"已存在相同名称的分组名", WaitTime = 1, ShowDateTime = false });
                    return;
                }
                sqLiteHelper.db.Insert(new ObjectGroup()
                {
                    ConnectionName = Connection.DbName,
                    DataBaseName = selectedDatabase.DbName,
                    GroupName = groupName,
                    OrderFlag = DateTime.Now
                });
            }
            BtnDelete.Visibility = Visibility.Collapsed;
            HidId.Text = "0";
            TextGourpName.Text = "";
            BtnSave.IsEnabled = false;
            var connKey = Connection.DbName;
            Task.Run(() =>
            {
                var datalist = sqLiteHelper.db.Table<ObjectGroup>().
                    Where(x => x.ConnectionName == connKey && x.DataBaseName == selectedDatabase.DbName).OrderBy(x => x.OrderFlag).ToList();
                Dispatcher.Invoke(() =>
                {
                    DataList = datalist;
                    if (ChangeRefreshEvent != null)
                    {
                        ChangeRefreshEvent();
                    }
                });
            });
        }

        /// <summary>
        /// 取消
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// 删除
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnDelete_OnClick(object sender, RoutedEventArgs e)
        {
            var sqLiteHelper = new SQLiteHelper();
            var groupId = Convert.ToInt32(HidId.Text);
            if (groupId < 1)
            {
                return;
            }
            var selectedDatabase = (DataBase)SelectDatabase.SelectedItem;
            var connKey = Connection.DbName;
            Task.Run(() =>
            {
                sqLiteHelper.db.Delete<ObjectGroup>(groupId);

                var list = sqLiteHelper.db.Table<SObjects>().Where(x =>
                    x.ConnectionName == connKey &&
                    x.DatabaseName == selectedDatabase.DbName &&
                    x.GroupId == groupId).ToList();
                if (list.Any())
                {
                    foreach (var sobj in list)
                    {
                        sqLiteHelper.db.Delete<SObjects>(sobj.Id);
                    }
                }
                var datalist = sqLiteHelper.db.Table<ObjectGroup>().
                    Where(x => x.ConnectionName == connKey && x.DataBaseName == selectedDatabase.DbName).ToList();
                Dispatcher.Invoke(() =>
                {
                    BtnDelete.Visibility = Visibility.Collapsed;
                    HidId.Text = "0";
                    TextGourpName.Text = "";
                    BtnSave.IsEnabled = false;
                    DataList = datalist;
                    if (ChangeRefreshEvent != null)
                    {
                        ChangeRefreshEvent();
                    }
                });
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void BtnReset_OnClick(object sender, RoutedEventArgs e)
        {
            BtnDelete.Visibility = Visibility.Collapsed;
            HidId.Text = "0";
            TextGourpName.Text = "";
            BtnSave.IsEnabled = false;
        }

        private void SelectDatabase_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            BtnDelete.Visibility = Visibility.Collapsed;
            var selectedDatabase = (DataBase)SelectDatabase.SelectedItem;
            var conn = Connection;
            Task.Run(() =>
            {
                var sqLiteHelper = new SQLiteHelper();
                var datalist = sqLiteHelper.db.Table<ObjectGroup>().
                    Where(x => x.ConnectionName == conn.DbName && x.DataBaseName == selectedDatabase.DbName).OrderBy(x => x.OrderFlag).ToList();
                Dispatcher.Invoke(() =>
                {
                    DataList = datalist;
                });
            });
        }

        private void TextGourpName_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TextGourpName.Text))
            {
                BtnSave.IsEnabled = true;
            }
        }

        private void ListGroup_OnDrop(object sender, DragEventArgs e)
        {
            var selectedDatabase = (DataBase)SelectDatabase.SelectedItem;
            var conn = Connection;
            var pos = e.GetPosition(ListGroup);
            var result = VisualTreeHelper.HitTest(ListGroup, pos);
            if (result == null)
            {
                return;
            }
            //查找元数据
            var sourceGroup = e.Data.GetData(typeof(ObjectGroup)) as ObjectGroup;
            if (sourceGroup == null)
            {
                return;
            }
            //查找目标数据
            var listBoxItem = Utils.FindVisualParent<ListBoxItem>(result.VisualHit);
            if (listBoxItem == null)
            {
                return;
            }
            var targetGroup = listBoxItem.Content as ObjectGroup;
            if (ReferenceEquals(targetGroup, sourceGroup))
            {
                return;
            }
            var sourceOrder = sourceGroup.OrderFlag;
            var targetOrder = targetGroup.OrderFlag;
            sourceGroup.OrderFlag = targetOrder;
            targetGroup.OrderFlag = sourceOrder;
            var sqLiteHelper = new SQLiteHelper();
            var listG = new List<ObjectGroup>()
            {
                sourceGroup,
                targetGroup
            };
            sqLiteHelper.db.UpdateAll(listG);
            var datalist = sqLiteHelper.db.Table<ObjectGroup>().
                Where(x => x.ConnectionName == conn.DbName && x.DataBaseName == selectedDatabase.DbName).
                OrderBy(x => x.OrderFlag).ToList();
            Dispatcher.Invoke(() =>
            {
                DataList = datalist;
                if (ChangeRefreshEvent != null)
                {
                    ChangeRefreshEvent();
                }
            });
        }

        private void ListGroup_OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (Mouse.LeftButton == MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(ListGroup);
                HitTestResult result = VisualTreeHelper.HitTest(ListGroup, pos);
                if (result == null)
                {
                    return;
                }
                var listBoxItem = Utils.FindVisualParent<ListBoxItem>(result.VisualHit);
                if (listBoxItem == null || listBoxItem.Content != ListGroup.SelectedItem)
                {
                    return;
                }
                DataObject dataObj = new DataObject(listBoxItem.Content as ObjectGroup);
                DragDrop.DoDragDrop(ListGroup, dataObj, DragDropEffects.Move);
            }
        }
    }
    internal static class Utils
    {
        //根据子元素查找父元素
        public static T FindVisualParent<T>(DependencyObject obj) where T : class
        {
            while (obj != null)
            {
                if (obj is T)
                    return obj as T;

                obj = VisualTreeHelper.GetParent(obj);
            }
            return null;
        }
    }
}