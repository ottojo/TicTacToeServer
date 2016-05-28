using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TicTacToeServer
{
    public partial class IpChooserDialog : Form
    {
        public IPAddress chosenAddress { get; set; }
        public IpChooserDialog(List<IPAddress> ipAddressList)
        {
            InitializeComponent();
            foreach(IPAddress ip in ipAddressList)
            {
                listBoxIPs.Items.Add(ip);
            }
            listBoxIPs.SelectedIndex = 0;
        }

        private void buttonOk_Click(object sender, EventArgs e)
        {
            this.chosenAddress = (IPAddress) listBoxIPs.SelectedItem;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        
    }
}
