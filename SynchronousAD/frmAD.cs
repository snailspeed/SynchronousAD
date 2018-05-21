﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.DirectoryServices;
using System.Security.Principal;

namespace SynchronousAD
{
    public partial class frmAD : Form
    {

        private List<AdModel> list = new List<AdModel>();

        public frmAD()
        {
            InitializeComponent();
        }

        #region## 同步按钮
        /// <summary>
        /// 功能：同步按钮
        /// 作者：Wilson
        /// 时间：2012-12-15
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSyns_Click(object sender, EventArgs e)
        {
            if (ValidationInput())
            {
                DirectoryEntry domain;
                DirectoryEntry rootOU;

                if (IsConnected(txtDomainName.Text.Trim(), txtUserName.Text.Trim(), txtPwd.Text.Trim(), out domain))
                {
                    if (IsExistOU(domain, out rootOU))
                    {
                        SyncAll(rootOU);      //同步所有                                         
                    }
                    else
                    {
                        MessageBox.Show("域中不存在此组织结构!", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                }
                else
                {
                    MessageBox.Show("不能连接到域,请确认输入是否正确!", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
        }
        #endregion

        #region## 同步
        /// <summary>
        /// 功能:同步
        /// 创建人:Wilson
        /// 创建时间:2012-12-15
        /// </summary>
        /// <param name="entryOU"></param>
        public void SyncAll(DirectoryEntry entryOU)
        {
            /*
             * 参考：http://msdn.microsoft.com/zh-cn/library/system.directoryservices.directorysearcher.filter(v=vs.80).aspx
             * 
             * -----------------其它------------------------------             
             * 机算机：       (objectCategory=computer)
             * 组：           (objectCategory=group)
             * 联系人：       (objectCategory=contact)
             * 共享文件夹：   (objectCategory=volume)
             * 打印机         (objectCategory=printQueue)
             * ---------------------------------------------------
             */
            DirectorySearcher mySearcher = new DirectorySearcher(entryOU, "(objectclass=organizationalUnit)"); //查询组织单位                 

            DirectoryEntry root = mySearcher.SearchRoot;   //查找根OU

            SyncRootOU(root);

            StringBuilder sb = new StringBuilder();

            sb.Append("\r\nID\t帐号\t类型\t父ID\r\n");

            foreach (var item in list)
            {
                sb.AppendFormat("{0}\t{1}\t{2}\t{3}\r\n", item.Id, item.Name, item.TypeId, item.ParentId);
            }

            LogRecord.WriteLog(sb.ToString());

            MessageBox.Show("同步成功", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);

            Application.Exit();
        }
        #endregion

        #region## 同步根组织单位
        /// <summary>
        /// 功能: 同步根组织单位
        /// 创建人:Wilson
        /// 创建时间:2012-12-15
        /// </summary>
        /// <param name="entry"></param>
        private void SyncRootOU(DirectoryEntry entry)
        {
            if (entry.Properties.Contains("ou") && entry.Properties.Contains("objectGUID"))
            {
                string rootOuName = entry.Properties["ou"][0].ToString();

                byte[] bGUID = entry.Properties["objectGUID"][0] as byte[];

                string id = BitConverter.ToString(bGUID);

                list.Add(new AdModel(id, rootOuName, (int)TypeEnum.OU, "0"));

                SyncSubOU(entry, id);
            }
        }
        #endregion

        #region## 同步下属组织单位及下属用户
        /// <summary>
        /// 功能: 同步下属组织单位及下属用户
        /// 创建人:Wilson
        /// 创建时间:2012-12-15
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="parentId"></param>
        private void SyncSubOU(DirectoryEntry entry, string parentId)
        {
            foreach (DirectoryEntry subEntry in entry.Children)
            {
                string entrySchemaClsName = subEntry.SchemaClassName;

                string[] arr = subEntry.Name.Split('=');
                string categoryStr = arr[0];
                string nameStr = arr[1];
                string id = string.Empty;

                if (subEntry.Properties.Contains("objectGUID"))   //SID
                {
                    byte[] bGUID = subEntry.Properties["objectGUID"][0] as byte[];

                    id = BitConverter.ToString(bGUID);
                }

                bool isExist = list.Exists(d => d.Id == id);

                switch (entrySchemaClsName)
                {
                    case "organizationalUnit":

                        if (!isExist)
                        {
                            list.Add(new AdModel(id, nameStr, (int)TypeEnum.OU, parentId));
                        }

                        SyncSubOU(subEntry, id);
                        break;
                    case "user":
                        string accountName = string.Empty;

                        if (subEntry.Properties.Contains("samaccountName"))
                        {
                            accountName = subEntry.Properties["samaccountName"][0].ToString();
                        }

                        if (!isExist)
                        {
                            list.Add(new AdModel(id, accountName, (int)TypeEnum.USER, parentId));
                        }
                        break;
                }
            }
        }
        #endregion


        //foreach (string property in subEntry.Properties.PropertyNames)
        //{
        //    LogRecord.WriteLog(string.Format("字段名: {0}   字段值：{1}\r\n", property, subEntry.Properties[property][0].ToString()));
        //}

        #region## 是否连接到域
        /// <summary>
        /// 功能：是否连接到域
        /// 作者：Wilson
        /// 时间：2012-12-15
        /// http://msdn.microsoft.com/zh-cn/library/system.directoryservices.directoryentry.path(v=vs.90).aspx
        /// </summary>
        /// <param name="domainName">域名或IP</param>
        /// <param name="userName">用户名</param>
        /// <param name="userPwd">密码</param>
        /// <param name="entry">域</param>
        /// <returns></returns>
        private bool IsConnected(string domainName, string userName, string userPwd, out DirectoryEntry domain)
        {
            domain = new DirectoryEntry();
            try
            {
                domain.Path = string.Format("LDAP://{0}", domainName);
                domain.Username = userName;
                domain.Password = userPwd;
                domain.AuthenticationType = AuthenticationTypes.Secure;
                domain.RefreshCache();

                return true;
            }
            catch (Exception ex)
            {
                LogRecord.WriteLog("[IsConnected方法]错误信息：" + ex.Message);
                return false;
            }
        }
        #endregion

        #region## 域中是否存在组织单位
        /// <summary>
        /// 功能：域中是否存在组织单位
        /// 作者：Wilson
        /// 时间：2012-12-15
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="ou"></param>
        /// <returns></returns>
        private bool IsExistOU(DirectoryEntry entry, out DirectoryEntry ou)
        {
            ou = new DirectoryEntry();
            try
            {
                ou = entry.Children.Find("OU=" + txtRootOU.Text.Trim());

                return (ou != null);
            }
            catch (Exception ex)
            {
                LogRecord.WriteLog("[IsExistOU方法]错误信息：" + ex.Message);
                return false;
            }
        }
        #endregion

        #region## 窗体加载
        /// <summary>
        /// 窗体加载
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void frmAD_Load(object sender, EventArgs e)
        {
            txtDomainName.Text = "192.168.206.103";
            txtUserName.Text = "administrator";
            txtPwd.Text = "P@ssw0rd";
            txtRootOU.Text = "acompany";
        }
        #endregion

        #region## 验证输入
        /// <summary>
        /// 功能：验证输入
        /// 作者：Wilson
        /// 时间：2012-12-15
        /// </summary>
        /// <returns></returns>
        private bool ValidationInput()
        {
            if (txtDomainName.Text.Trim().Length == 0)
            {
                MessageBox.Show("请输入域名!", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtDomainName.Focus();
                return false;
            }

            if (txtUserName.Text.Trim().Length == 0)
            {
                MessageBox.Show("请输入用户名!", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtUserName.Focus();
                return false;
            }

            if (txtPwd.Text.Trim().Length == 0)
            {
                MessageBox.Show("请输入密码!", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtPwd.Focus();
                return false;
            }

            if (txtRootOU.Text.Trim().Length == 0)
            {
                MessageBox.Show("请输入根组织单位!", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtRootOU.Focus();
                return false;
            }
            return true;
        }
        #endregion
    }


    public enum TypeEnum : int
    {
        /// <summary>
        /// 组织单位
        /// </summary>
        OU = 1,

        /// <summary>
        /// 用户
        /// </summary>
        USER = 2
    }

    #region## Ad域实体
    /// <summary>
    /// Ad域实体
    /// </summary>
    public class AdModel
    {
        public AdModel(string id, string name, int typeId, string parentId)
        {
            Id = id;
            Name = name;
            TypeId = typeId;
            ParentId = parentId;
        }

        public string Id { get; set; }

        public string Name { get; set; }

        public int TypeId { get; set; }

        public string ParentId { get; set; }
    }
    #endregion
}
