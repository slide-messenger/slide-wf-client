using System.Net;
using System.Net.Http.Json;
using WinFormsClient.Api;
using WinFormsClient.GuiHandlers;
using Server.Entities;
using System.Collections.Generic;
using System;
using System.Security.Cryptography;
using static System.Windows.Forms.LinkLabel;
using Server.Bodies;

namespace WinFormsClient
{
    public partial class Main : Form
    {
        public Auth? AuthForm;
        public Registration? RegistrationForm;

        public readonly GeneralGui GeneralHandler;
        public readonly UsersGui UsersHandler;
        public readonly MessagesGui MessagesHandler;

        public Main()
        {
            InitializeComponent();

            GeneralHandler = new(this);
            UsersHandler = new(this);
            MessagesHandler = new(this);
        }
        public async void QuitUser()
        {
            UpdateTimer.Stop();

            UsersHandler.CurrentUser = new();
            MessagesHandler.CurrentChatId = 0;
            MessagesHandler.ChatIds.Clear();
            MessagesHandler.Chats.Clear();
            LBChats.Items.Clear();
            RTBTypeMessage.Text = "�������� ���������...";
            RTBMessages.Clear();

            if (AuthForm is null || AuthForm.IsDisposed)
            {
                AuthForm = new() { MainForm = this };
            }
            if (RegistrationForm is null || RegistrationForm.IsDisposed)
            {
                RegistrationForm = new() { MainForm = this };
            }

            Hide();
            DialogResult res = AuthForm.ShowDialog();

            while (res != DialogResult.OK)
            {
                if (res != DialogResult.Continue)
                {
                    Application.Exit();
                    return;
                }
                res = AuthForm.ShowDialog();
            }

            Show();
            AuthForm.Close();
            if (!await UpdateUser()) { return; }
            if (!await UpdateChats()) { return; }
            UpdateTimer.Enabled = true;
        }
        public void ShowError(string desc = "������ ����������")
        {
            MessageBox.Show(this, $"{desc}",
                "SlideMessenger", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        public void ShowCriticalError(string desc = "������ ����������!")
        {
            UpdateTimer.Stop();
            if (MessageBox.Show($"��������� ������. ���: {desc}",
                "SlideMessenger", MessageBoxButtons.OK, MessageBoxIcon.Error) ==
                DialogResult.OK)
            {
                QuitUser();
            }
        }
        public void ShowErrorAsync(string desc = "������ ����������!")
        {
            UpdateTimer.Stop();
            LabelChatname.Text = desc;
            LabelChatname.ForeColor = Color.Red;
        }
        public async Task<bool> UpdateUser(string? username = null)
        {
            if (!await UsersHandler.UpdateCurrentUser(username))
            {
                return false;
            }

            LabelFirstName.Text = UsersHandler.CurrentUser.FirstName;
            LabelLastName.Text = UsersHandler.CurrentUser.LastName;
            LabelChatname.ForeColor = DefaultForeColor;
            LabelChatname.Text = "�������� ���";

            return true;
        }
        public async Task<bool> UpdateChats()
        {
            LBChats.Items.Clear();
            if (await MessagesHandler.UpdateChats())
            {
                foreach (var chat in MessagesHandler.Chats)
                {
                    LBChats.Items.Add(chat.Value.ToString());
                }
                return true;
            }
            return false;
        }
        private async Task<bool> UpdateMessages()
        {
            if (MessagesHandler.CurrentChatId == 0) { return true; }
            RTBMessages.Clear();
            if (await MessagesHandler.UpdateMessages())
            {
                foreach (var msg in MessagesHandler.CurrentMessages)
                {
                    RTBMessages.SelectionColor = msg.SenderId == UsersHandler.CurrentUser.UserId ?
                        Color.DodgerBlue : Color.Black;
                    RTBMessages.SelectionFont = new Font(RTBMessages.Font, FontStyle.Bold);
                    RTBMessages.AppendText($"{msg.Sender} <{msg.SentAt}>: ");
                    RTBMessages.SelectionFont = new Font(RTBMessages.Font, FontStyle.Regular);
                    RTBMessages.AppendText($"{msg.Content}" + Environment.NewLine);
                }

                RTBMessages.SelectionStart = RTBMessages.TextLength;
                RTBMessages.ScrollToCaret();

                LabelChatname.Text = MessagesHandler.
                    Chats[MessagesHandler.CurrentChatId].Name;

                return true;
            }
            return false;
        }

        private async void SendButton_Click(object sender, EventArgs e)
        {
            var content = RTBTypeMessage.Text;
            if (string.IsNullOrWhiteSpace(content) || MessagesHandler.CurrentChatId == 0) { return; }
            if (await MessagesHandler.Send(new Server.Entities.Message
            (
                MessagesHandler.CurrentChatId,
                UsersHandler.CurrentUser.UserId,
                content,
                DateTime.UtcNow)))
            {
                RTBTypeMessage.Clear();
                await UpdateMessages();
                await UpdateChats();
            }
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            var self = (System.Windows.Forms.Timer)sender;
            var get = new Func<Task>(async () =>
            {
                self.Stop();
                var res = await MessagesHandler.CheckForNewMessages();
                switch (res)
                {
                    case 0:
                        break;
                    case 1:
                        await UpdateMessages();
                        break;
                    case 2:
                        return;
                }
                res = await MessagesHandler.CheckForUnreadChats();
                switch (res)
                {
                    case 0:
                        break;
                    case 1:
                        await UpdateChats();
                        break;
                    case 2:
                        return;
                }
                self.Start();
            });
            get.Invoke();
        }
        private void Main_Load(object sender, EventArgs e)
        {
            QuitUser();
        }

        private void MessageRTB_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                SendButton_Click(sender, e);
            }
        }

        private async void LBChats_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (LBChats.SelectedIndex >= 0)
            {
                MessagesHandler.CurrentChatId = MessagesHandler.ChatIds[LBChats.SelectedIndex];
            }
            await UpdateMessages();
            await UpdateChats();
        }
        private void MessageRTB_Click(object sender, EventArgs e)
        {
            var self = (RichTextBox)sender;
            if (self.Text == "�������� ���������...")
            {
                self.Clear();
            }
        }

        private void LinkQuit_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            QuitUser();
        }

        private void ButtonProfileInfo_Click(object sender, EventArgs e)
        {
            MessageBox.Show(UsersHandler.CurrentUser.ToString(), "��� �������", MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private async void ButtonJoinChat_Click(object sender, EventArgs e)
        {
            using var dlg = new InputBox();
            if (dlg.Execute("������� ������ �� ���:", 64) == DialogResult.OK)
            {
                var link = dlg.Result;
                var isDialog = link.StartsWith("uid=");
                var isGroupChat = link.StartsWith("cid=");
                if (link.Length < 5 || (!isDialog && !isGroupChat))
                {
                    MessageBox.Show("�������� ������ ������",
                        "SliderMessenger",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (!int.TryParse(link[4..], out int id))
                {
                    MessageBox.Show("�������� ������ ������",
                        "SliderMessenger",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (isDialog)
                {
                    if (await MessagesHandler.StartDialog(id))
                    {
                        await UpdateChats();
                    }
                    return;
                }
                if (await MessagesHandler.JoinChat(id))
                {
                    await UpdateChats();
                }
            }
        }

        private async void ButtonCreateChat_Click(object sender, EventArgs e)
        {
            using var dlg = new InputBox();
            if (dlg.Execute("���������� �������� ����: ", 32) == DialogResult.OK)
            {
                if (await MessagesHandler.CreateGroupChat(dlg.Result))
                {
                    await UpdateMessages();
                    await UpdateChats();
                }
            }
        }

        private void ButtonGetLink_Click(object sender, EventArgs e)
        {
            if (MessagesHandler.CurrentChatId == 0)
            {
                MessageBox.Show("��� �� ������!",
                "SlideMessenger", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            var chat = MessagesHandler.Chats[MessagesHandler.CurrentChatId];
            string link;
            if (chat.SecondId == 0)
            {
                link = $"cid={chat.ChatId}";
                MessageBox.Show($"������ �� ��� ����������� � ����� ������:\n{link}", "�������� ������",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                link = $"uid={(UsersHandler.CurrentUser.UserId == chat.FirstId ? chat.SecondId : chat.FirstId)}";
                MessageBox.Show($"������ �� ����������� ����������� � ����� ������:\n{link}", "�������� ������",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            Clipboard.SetText(link);
        }

        private void ButtonCopyMyLink_Click(object sender, EventArgs e)
        {
            MessageBox.Show("������ ������ ����������� � ����� ������", "������ ������",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            Clipboard.SetText($"uid={UsersHandler.CurrentUser.UserId}");
        }

        private void LabelChatname_Click(object sender, EventArgs e)
        {

        }

        private void groupBox4_Enter(object sender, EventArgs e)
        {

        }

        private void groupBox3_Enter(object sender, EventArgs e)
        {

        }
    }
}
