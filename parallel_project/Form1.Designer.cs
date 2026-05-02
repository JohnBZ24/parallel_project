namespace parallel_project
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        /// <remarks>
        /// Logic: Instantiates controls, sets properties (size/colors/fonts), and wires UI events to handlers
        /// defined in Form1.cs.
        /// </remarks>
        private void InitializeComponent()
        {
            btnHostGame = new Button();
            btnJoinGame = new Button();
            btnCancelSearch = new Button();
            btnLoadTeam = new Button();
            btnStartFight = new Button();
            btnAttackRace = new Button();
            lblStatus = new Label();
            lblCountdown = new Label();
            lblPlayerRole = new Label();
            lblTopPlayer = new Label();
            lblBottomPlayer = new Label();
            lstLog = new ListBox();
            pnlArena = new Panel();
            hpTopWarrior = new ProgressBar();
            hpTopMage = new ProgressBar();
            hpTopArcher = new ProgressBar();
            hpBottomWarrior = new ProgressBar();
            hpBottomMage = new ProgressBar();
            hpBottomArcher = new ProgressBar();
            picTopWarrior = new PictureBox();
            picTopMage = new PictureBox();
            picTopArcher = new PictureBox();
            picBottomWarrior = new PictureBox();
            picBottomMage = new PictureBox();
            picBottomArcher = new PictureBox();
            picEffect = new PictureBox();
            pnlArena.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)picTopWarrior).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picTopMage).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picTopArcher).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picBottomWarrior).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picBottomMage).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picBottomArcher).BeginInit();
            ((System.ComponentModel.ISupportInitialize)picEffect).BeginInit();
            SuspendLayout();
            // 
            // btnHostGame
            // 
            btnHostGame.BackColor = Color.FromArgb(40, 40, 44);
            btnHostGame.FlatStyle = FlatStyle.Flat;
            btnHostGame.ForeColor = Color.Gainsboro;
            btnHostGame.Location = new Point(902, 392);
            btnHostGame.Name = "btnHostGame";
            btnHostGame.Size = new Size(160, 34);
            btnHostGame.TabIndex = 0;
            btnHostGame.Text = "Host Game";
            btnHostGame.UseVisualStyleBackColor = false;
            btnHostGame.Click += btnHostGame_Click;
            // 
            // btnJoinGame
            // 
            btnJoinGame.BackColor = Color.FromArgb(40, 40, 44);
            btnJoinGame.FlatStyle = FlatStyle.Flat;
            btnJoinGame.ForeColor = Color.Gainsboro;
            btnJoinGame.Location = new Point(902, 432);
            btnJoinGame.Name = "btnJoinGame";
            btnJoinGame.Size = new Size(160, 34);
            btnJoinGame.TabIndex = 1;
            btnJoinGame.Text = "Join Game";
            btnJoinGame.UseVisualStyleBackColor = false;
            btnJoinGame.Click += btnJoinGame_Click;
            // 
            // btnCancelSearch
            // 
            btnCancelSearch.BackColor = Color.FromArgb(62, 36, 36);
            btnCancelSearch.Enabled = false;
            btnCancelSearch.FlatStyle = FlatStyle.Flat;
            btnCancelSearch.ForeColor = Color.Gainsboro;
            btnCancelSearch.Location = new Point(902, 472);
            btnCancelSearch.Name = "btnCancelSearch";
            btnCancelSearch.Size = new Size(160, 34);
            btnCancelSearch.TabIndex = 2;
            btnCancelSearch.Text = "Cancel";
            btnCancelSearch.UseVisualStyleBackColor = false;
            btnCancelSearch.Click += btnCancelSearch_Click;
            // 
            // btnLoadTeam
            // 
            btnLoadTeam.BackColor = Color.FromArgb(40, 40, 44);
            btnLoadTeam.Enabled = false;
            btnLoadTeam.FlatStyle = FlatStyle.Flat;
            btnLoadTeam.ForeColor = Color.Gainsboro;
            btnLoadTeam.Location = new Point(902, 526);
            btnLoadTeam.Name = "btnLoadTeam";
            btnLoadTeam.Size = new Size(160, 34);
            btnLoadTeam.TabIndex = 3;
            btnLoadTeam.Text = "Load Team";
            btnLoadTeam.UseVisualStyleBackColor = false;
            btnLoadTeam.Click += btnLoadTeam_Click;
            // 
            // btnStartFight
            // 
            btnStartFight.BackColor = Color.FromArgb(40, 40, 44);
            btnStartFight.Enabled = false;
            btnStartFight.FlatStyle = FlatStyle.Flat;
            btnStartFight.ForeColor = Color.Gainsboro;
            btnStartFight.Location = new Point(902, 566);
            btnStartFight.Name = "btnStartFight";
            btnStartFight.Size = new Size(160, 34);
            btnStartFight.TabIndex = 4;
            btnStartFight.Text = "Start Fight";
            btnStartFight.UseVisualStyleBackColor = false;
            btnStartFight.Click += btnStartFight_Click;
            // 
            // btnAttackRace
            // 
            btnAttackRace.BackColor = Color.FromArgb(41, 55, 35);
            btnAttackRace.Enabled = false;
            btnAttackRace.FlatStyle = FlatStyle.Flat;
            btnAttackRace.Font = new Font("Consolas", 11F, FontStyle.Bold);
            btnAttackRace.ForeColor = Color.Gainsboro;
            btnAttackRace.Location = new Point(902, 606);
            btnAttackRace.Name = "btnAttackRace";
            btnAttackRace.Size = new Size(160, 42);
            btnAttackRace.TabIndex = 5;
            btnAttackRace.Text = "ATTACK RACE";
            btnAttackRace.UseVisualStyleBackColor = false;
            btnAttackRace.Click += btnAttackRace_Click;
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Font = new Font("Consolas", 10F, FontStyle.Bold);
            lblStatus.ForeColor = Color.Gainsboro;
            lblStatus.Location = new Point(902, 26);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(117, 20);
            lblStatus.TabIndex = 6;
            lblStatus.Text = "Status: Idle";
            // 
            // lblCountdown
            // 
            lblCountdown.AutoSize = true;
            lblCountdown.BackColor = Color.Transparent;
            lblCountdown.Font = new Font("Consolas", 36F, FontStyle.Bold);
            lblCountdown.ForeColor = Color.FromArgb(255, 230, 150);
            lblCountdown.Location = new Point(180, 204);
            lblCountdown.Name = "lblCountdown";
            lblCountdown.Size = new Size(0, 70);
            lblCountdown.TabIndex = 7;
            // 
            // lblPlayerRole
            // 
            lblPlayerRole.AutoSize = true;
            lblPlayerRole.Font = new Font("Consolas", 10F);
            lblPlayerRole.ForeColor = Color.Gainsboro;
            lblPlayerRole.Location = new Point(902, 56);
            lblPlayerRole.Name = "lblPlayerRole";
            lblPlayerRole.Size = new Size(81, 20);
            lblPlayerRole.TabIndex = 8;
            lblPlayerRole.Text = "Role: --";
            // 
            // lblTopPlayer
            // 
            lblTopPlayer.AutoSize = true;
            lblTopPlayer.Font = new Font("Consolas", 10F);
            lblTopPlayer.ForeColor = Color.Gainsboro;
            lblTopPlayer.Location = new Point(902, 96);
            lblTopPlayer.Name = "lblTopPlayer";
            lblTopPlayer.Size = new Size(126, 20);
            lblTopPlayer.TabIndex = 9;
            lblTopPlayer.Text = "Top: Player 1";
            // 
            // lblBottomPlayer
            // 
            lblBottomPlayer.AutoSize = true;
            lblBottomPlayer.Font = new Font("Consolas", 10F);
            lblBottomPlayer.ForeColor = Color.Gainsboro;
            lblBottomPlayer.Location = new Point(902, 126);
            lblBottomPlayer.Name = "lblBottomPlayer";
            lblBottomPlayer.Size = new Size(153, 20);
            lblBottomPlayer.TabIndex = 10;
            lblBottomPlayer.Text = "Bottom: Player 2";
            // 
            // lstLog
            // 
            lstLog.BackColor = Color.FromArgb(18, 18, 20);
            lstLog.BorderStyle = BorderStyle.FixedSingle;
            lstLog.Font = new Font("Consolas", 9F);
            lstLog.ForeColor = Color.Gainsboro;
            lstLog.FormattingEnabled = true;
            lstLog.ItemHeight = 18;
            lstLog.Location = new Point(18, 26);
            lstLog.Name = "lstLog";
            lstLog.Size = new Size(330, 614);
            lstLog.TabIndex = 11;
            // 
            // pnlArena
            // 
            pnlArena.BackColor = Color.FromArgb(24, 24, 28);
            pnlArena.BorderStyle = BorderStyle.FixedSingle;
            pnlArena.Controls.Add(lblCountdown);
            pnlArena.Controls.Add(hpTopWarrior);
            pnlArena.Controls.Add(hpTopMage);
            pnlArena.Controls.Add(hpTopArcher);
            pnlArena.Controls.Add(hpBottomWarrior);
            pnlArena.Controls.Add(hpBottomMage);
            pnlArena.Controls.Add(hpBottomArcher);
            pnlArena.Controls.Add(picTopWarrior);
            pnlArena.Controls.Add(picTopMage);
            pnlArena.Controls.Add(picTopArcher);
            pnlArena.Controls.Add(picBottomWarrior);
            pnlArena.Controls.Add(picBottomMage);
            pnlArena.Controls.Add(picBottomArcher);
            pnlArena.Controls.Add(picEffect);
            pnlArena.Location = new Point(366, 26);
            pnlArena.Name = "pnlArena";
            pnlArena.Size = new Size(520, 614);
            pnlArena.TabIndex = 12;
            // 
            // hpTopWarrior
            // 
            hpTopWarrior.Location = new Point(40, 96);
            hpTopWarrior.Name = "hpTopWarrior";
            hpTopWarrior.Size = new Size(120, 14);
            hpTopWarrior.TabIndex = 0;
            // 
            // hpTopMage
            // 
            hpTopMage.Location = new Point(200, 96);
            hpTopMage.Name = "hpTopMage";
            hpTopMage.Size = new Size(120, 14);
            hpTopMage.TabIndex = 1;
            // 
            // hpTopArcher
            // 
            hpTopArcher.Location = new Point(360, 96);
            hpTopArcher.Name = "hpTopArcher";
            hpTopArcher.Size = new Size(120, 14);
            hpTopArcher.TabIndex = 2;
            // 
            // hpBottomWarrior
            // 
            hpBottomWarrior.Location = new Point(40, 504);
            hpBottomWarrior.Name = "hpBottomWarrior";
            hpBottomWarrior.Size = new Size(120, 14);
            hpBottomWarrior.TabIndex = 3;
            // 
            // hpBottomMage
            // 
            hpBottomMage.Location = new Point(200, 504);
            hpBottomMage.Name = "hpBottomMage";
            hpBottomMage.Size = new Size(120, 14);
            hpBottomMage.TabIndex = 4;
            // 
            // hpBottomArcher
            // 
            hpBottomArcher.Location = new Point(360, 504);
            hpBottomArcher.Name = "hpBottomArcher";
            hpBottomArcher.Size = new Size(120, 14);
            hpBottomArcher.TabIndex = 5;
            // 
            // picTopWarrior
            // 
            picTopWarrior.BackColor = Color.FromArgb(30, 30, 34);
            picTopWarrior.Location = new Point(64, 24);
            picTopWarrior.Name = "picTopWarrior";
            picTopWarrior.Size = new Size(72, 72);
            picTopWarrior.SizeMode = PictureBoxSizeMode.StretchImage;
            picTopWarrior.TabIndex = 6;
            picTopWarrior.TabStop = false;
            picTopWarrior.Click += picTopWarrior_Click;
            // 
            // picTopMage
            // 
            picTopMage.BackColor = Color.FromArgb(30, 30, 34);
            picTopMage.Location = new Point(224, 24);
            picTopMage.Name = "picTopMage";
            picTopMage.Size = new Size(72, 72);
            picTopMage.SizeMode = PictureBoxSizeMode.StretchImage;
            picTopMage.TabIndex = 7;
            picTopMage.TabStop = false;
            picTopMage.Click += picTopMage_Click;
            // 
            // picTopArcher
            // 
            picTopArcher.BackColor = Color.FromArgb(30, 30, 34);
            picTopArcher.Location = new Point(384, 24);
            picTopArcher.Name = "picTopArcher";
            picTopArcher.Size = new Size(72, 72);
            picTopArcher.SizeMode = PictureBoxSizeMode.StretchImage;
            picTopArcher.TabIndex = 8;
            picTopArcher.TabStop = false;
            picTopArcher.Click += picTopArcher_Click;
            // 
            // picBottomWarrior
            // 
            picBottomWarrior.BackColor = Color.FromArgb(30, 30, 34);
            picBottomWarrior.Location = new Point(64, 520);
            picBottomWarrior.Name = "picBottomWarrior";
            picBottomWarrior.Size = new Size(72, 72);
            picBottomWarrior.SizeMode = PictureBoxSizeMode.StretchImage;
            picBottomWarrior.TabIndex = 9;
            picBottomWarrior.TabStop = false;
            picBottomWarrior.Click += picBottomWarrior_Click;
            // 
            // picBottomMage
            // 
            picBottomMage.BackColor = Color.FromArgb(30, 30, 34);
            picBottomMage.Location = new Point(224, 520);
            picBottomMage.Name = "picBottomMage";
            picBottomMage.Size = new Size(72, 72);
            picBottomMage.SizeMode = PictureBoxSizeMode.StretchImage;
            picBottomMage.TabIndex = 10;
            picBottomMage.TabStop = false;
            picBottomMage.Click += picBottomMage_Click;
            // 
            // picBottomArcher
            // 
            picBottomArcher.BackColor = Color.FromArgb(30, 30, 34);
            picBottomArcher.Location = new Point(384, 520);
            picBottomArcher.Name = "picBottomArcher";
            picBottomArcher.Size = new Size(72, 72);
            picBottomArcher.SizeMode = PictureBoxSizeMode.StretchImage;
            picBottomArcher.TabIndex = 11;
            picBottomArcher.TabStop = false;
            picBottomArcher.Click += picBottomArcher_Click;
            // 
            // picEffect
            // 
            picEffect.BackColor = Color.Transparent;
            picEffect.Location = new Point(244, 292);
            picEffect.Name = "picEffect";
            picEffect.Size = new Size(32, 32);
            picEffect.SizeMode = PictureBoxSizeMode.StretchImage;
            picEffect.TabIndex = 12;
            picEffect.TabStop = false;
            picEffect.Visible = false;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(14, 14, 16);
            ClientSize = new Size(1074, 670);
            Controls.Add(pnlArena);
            Controls.Add(lstLog);
            Controls.Add(lblBottomPlayer);
            Controls.Add(lblTopPlayer);
            Controls.Add(lblPlayerRole);
            Controls.Add(lblStatus);
            Controls.Add(btnAttackRace);
            Controls.Add(btnStartFight);
            Controls.Add(btnLoadTeam);
            Controls.Add(btnCancelSearch);
            Controls.Add(btnJoinGame);
            Controls.Add(btnHostGame);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Name = "Form1";
            Text = "Arena of Heroes Pixel Edition";
            Load += Form1_Load;
            pnlArena.ResumeLayout(false);
            pnlArena.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)picTopWarrior).EndInit();
            ((System.ComponentModel.ISupportInitialize)picTopMage).EndInit();
            ((System.ComponentModel.ISupportInitialize)picTopArcher).EndInit();
            ((System.ComponentModel.ISupportInitialize)picBottomWarrior).EndInit();
            ((System.ComponentModel.ISupportInitialize)picBottomMage).EndInit();
            ((System.ComponentModel.ISupportInitialize)picBottomArcher).EndInit();
            ((System.ComponentModel.ISupportInitialize)picEffect).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnHostGame;
        private Button btnJoinGame;
        private Button btnCancelSearch;
        private Button btnLoadTeam;
        private Button btnStartFight;
        private Button btnAttackRace;
        private Label lblStatus;
        private Label lblCountdown;
        private Label lblPlayerRole;
        private Label lblTopPlayer;
        private Label lblBottomPlayer;
        private ListBox lstLog;
        private Panel pnlArena;
        private PictureBox picTopWarrior;
        private PictureBox picTopMage;
        private PictureBox picTopArcher;
        private PictureBox picBottomWarrior;
        private PictureBox picBottomMage;
        private PictureBox picBottomArcher;
        private ProgressBar hpTopWarrior;
        private ProgressBar hpTopMage;
        private ProgressBar hpTopArcher;
        private ProgressBar hpBottomWarrior;
        private ProgressBar hpBottomMage;
        private ProgressBar hpBottomArcher;
        private PictureBox picEffect;
    }
}
