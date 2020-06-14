namespace S4L_Login
{
    partial class LoginWindow
    {
        /// <summary>
        /// Erforderliche Designervariable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Verwendete Ressourcen bereinigen.
        /// </summary>
        /// <param name="disposing">True, wenn verwaltete Ressourcen gelöscht werden sollen; andernfalls False.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Vom Windows Form-Designer generierter Code

        /// <summary>
        /// Erforderliche Methode für die Designerunterstützung.
        /// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LoginWindow));
            this.dbgtx = new System.Windows.Forms.Label();
            this.button1 = new System.Windows.Forms.Button();
            this.login_username = new System.Windows.Forms.TextBox();
            this.login_passwd = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.start_btn = new System.Windows.Forms.Button();
            this.label4 = new System.Windows.Forms.Label();
            this.errtx_label = new System.Windows.Forms.Label();
            this.checkBox2 = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // dbgtx
            // 
            this.dbgtx.AutoSize = true;
            this.dbgtx.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.dbgtx.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dbgtx.Location = new System.Drawing.Point(8, 159);
            this.dbgtx.Name = "dbgtx";
            this.dbgtx.Size = new System.Drawing.Size(41, 16);
            this.dbgtx.TabIndex = 0;
            this.dbgtx.Text = "dbgtx";
            // 
            // button1
            // 
            this.button1.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.button1.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.button1.Location = new System.Drawing.Point(1, 186);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(444, 40);
            this.button1.TabIndex = 1;
            this.button1.Text = "Login";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // login_username
            // 
            this.login_username.BackColor = System.Drawing.Color.White;
            this.login_username.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.login_username.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.login_username.ImeMode = System.Windows.Forms.ImeMode.Disable;
            this.login_username.Location = new System.Drawing.Point(23, 50);
            this.login_username.MaxLength = 16;
            this.login_username.Name = "login_username";
            this.login_username.Size = new System.Drawing.Size(400, 23);
            this.login_username.TabIndex = 2;
            this.login_username.WordWrap = false;
            this.login_username.KeyDown += new System.Windows.Forms.KeyEventHandler(this.login_username_KeyDown);
            // 
            // login_passwd
            // 
            this.login_passwd.BackColor = System.Drawing.Color.White;
            this.login_passwd.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.login_passwd.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.login_passwd.ImeMode = System.Windows.Forms.ImeMode.Disable;
            this.login_passwd.Location = new System.Drawing.Point(23, 108);
            this.login_passwd.MaxLength = 16;
            this.login_passwd.Name = "login_passwd";
            this.login_passwd.Size = new System.Drawing.Size(400, 23);
            this.login_passwd.TabIndex = 3;
            this.login_passwd.UseSystemPasswordChar = true;
            this.login_passwd.WordWrap = false;
            this.login_passwd.KeyDown += new System.Windows.Forms.KeyEventHandler(this.login_passwd_KeyDown);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(188, 31);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(77, 18);
            this.label1.TabIndex = 4;
            this.label1.Text = "Username";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F);
            this.label2.Location = new System.Drawing.Point(189, 89);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(75, 18);
            this.label2.TabIndex = 5;
            this.label2.Text = "Password";
            // 
            // start_btn
            // 
            this.start_btn.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.start_btn.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.start_btn.Location = new System.Drawing.Point(1, 186);
            this.start_btn.Name = "start_btn";
            this.start_btn.Size = new System.Drawing.Size(444, 40);
            this.start_btn.TabIndex = 12;
            this.start_btn.Text = "Start";
            this.start_btn.UseVisualStyleBackColor = true;
            this.start_btn.Click += new System.EventHandler(this.start_btn_Click);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F);
            this.label4.Location = new System.Drawing.Point(141, 192);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(175, 29);
            this.label4.TabIndex = 16;
            this.label4.Text = "Authenticating..";
            this.label4.Click += new System.EventHandler(this.label4_Click);
            // 
            // errtx_label
            // 
            this.errtx_label.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.errtx_label.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.errtx_label.ForeColor = System.Drawing.Color.Maroon;
            this.errtx_label.Location = new System.Drawing.Point(23, 146);
            this.errtx_label.Name = "errtx_label";
            this.errtx_label.Size = new System.Drawing.Size(400, 15);
            this.errtx_label.TabIndex = 17;
            this.errtx_label.Text = "errtx";
            this.errtx_label.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // checkBox2
            // 
            this.checkBox2.AutoSize = true;
            this.checkBox2.Enabled = false;
            this.checkBox2.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.checkBox2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F);
            this.checkBox2.Location = new System.Drawing.Point(375, 144);
            this.checkBox2.Name = "checkBox2";
            this.checkBox2.Size = new System.Drawing.Size(56, 20);
            this.checkBox2.TabIndex = 19;
            this.checkBox2.Text = "Save";
            this.checkBox2.UseVisualStyleBackColor = true;
            // 
            // LoginWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BorderStyle = MetroFramework.Forms.MetroFormBorderStyle.FixedSingle;
            this.ClientSize = new System.Drawing.Size(446, 231);
            this.Controls.Add(this.checkBox2);
            this.Controls.Add(this.errtx_label);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.login_passwd);
            this.Controls.Add(this.login_username);
            this.Controls.Add(this.dbgtx);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.start_btn);
            this.ForeColor = System.Drawing.Color.White;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "LoginWindow";
            this.Resizable = false;
            this.Style = MetroFramework.MetroColorStyle.White;
            this.TextAlign = MetroFramework.Forms.MetroFormTextAlign.Center;
            this.Theme = MetroFramework.MetroThemeStyle.Dark;
            this.TopMost = true;
            this.TransparencyKey = System.Drawing.Color.Fuchsia;
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Form1_FormClosed);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.MouseDown += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseDown);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label dbgtx;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.TextBox login_username;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox login_passwd;
        private System.Windows.Forms.Button start_btn;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label errtx_label;
        private System.Windows.Forms.CheckBox checkBox2;
    }
}

