namespace drawprogram
{
    partial class Form1
    {
        /// <summary>
        /// Variable nécessaire au concepteur.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Nettoyage des ressources utilisées.
        /// </summary>
        /// <param name="disposing">true si les ressources managées doivent être supprimées ; sinon, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Code généré par le Concepteur Windows Form

        /// <summary>
        /// Méthode requise pour la prise en charge du concepteur - ne modifiez pas
        /// le contenu de cette méthode avec l'éditeur de code.
        /// </summary>
        private void InitializeComponent()
        {
            this.mouseXYinfo = new System.Windows.Forms.Label();
            this.pixelCountInfo = new System.Windows.Forms.Label();
            this.checkpixelstateBTN = new System.Windows.Forms.Button();
            this.proccessingpixelInfo = new System.Windows.Forms.Label();
            this.validationInfo = new System.Windows.Forms.TextBox();
            this.myUPOInfo = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // mouseXYinfo
            // 
            this.mouseXYinfo.AutoSize = true;
            this.mouseXYinfo.Location = new System.Drawing.Point(60, 428);
            this.mouseXYinfo.Name = "mouseXYinfo";
            this.mouseXYinfo.Size = new System.Drawing.Size(22, 13);
            this.mouseXYinfo.TabIndex = 0;
            this.mouseXYinfo.Text = "0:0";
            // 
            // pixelCountInfo
            // 
            this.pixelCountInfo.AutoSize = true;
            this.pixelCountInfo.Location = new System.Drawing.Point(697, 389);
            this.pixelCountInfo.Name = "pixelCountInfo";
            this.pixelCountInfo.Size = new System.Drawing.Size(13, 13);
            this.pixelCountInfo.TabIndex = 1;
            this.pixelCountInfo.Text = "0";
            // 
            // checkpixelstateBTN
            // 
            this.checkpixelstateBTN.Location = new System.Drawing.Point(700, 13);
            this.checkpixelstateBTN.Name = "checkpixelstateBTN";
            this.checkpixelstateBTN.Size = new System.Drawing.Size(75, 23);
            this.checkpixelstateBTN.TabIndex = 2;
            this.checkpixelstateBTN.Text = "pixel state";
            this.checkpixelstateBTN.UseVisualStyleBackColor = true;
            this.checkpixelstateBTN.Click += new System.EventHandler(this.checkpixelstateBTN_Click);
            // 
            // proccessingpixelInfo
            // 
            this.proccessingpixelInfo.AutoSize = true;
            this.proccessingpixelInfo.Location = new System.Drawing.Point(697, 415);
            this.proccessingpixelInfo.Name = "proccessingpixelInfo";
            this.proccessingpixelInfo.Size = new System.Drawing.Size(13, 13);
            this.proccessingpixelInfo.TabIndex = 3;
            this.proccessingpixelInfo.Text = "0";
            // 
            // validationInfo
            // 
            this.validationInfo.Location = new System.Drawing.Point(660, 56);
            this.validationInfo.Multiline = true;
            this.validationInfo.Name = "validationInfo";
            this.validationInfo.Size = new System.Drawing.Size(128, 294);
            this.validationInfo.TabIndex = 4;
            // 
            // myUPOInfo
            // 
            this.myUPOInfo.AutoSize = true;
            this.myUPOInfo.Location = new System.Drawing.Point(300, 428);
            this.myUPOInfo.Name = "myUPOInfo";
            this.myUPOInfo.Size = new System.Drawing.Size(35, 13);
            this.myUPOInfo.TabIndex = 5;
            this.myUPOInfo.Text = "label1";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.myUPOInfo);
            this.Controls.Add(this.validationInfo);
            this.Controls.Add(this.proccessingpixelInfo);
            this.Controls.Add(this.checkpixelstateBTN);
            this.Controls.Add(this.pixelCountInfo);
            this.Controls.Add(this.mouseXYinfo);
            this.KeyPreview = true;
            this.Name = "Form1";
            this.Text = "PAINTMYBLOCKCHAIN";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label mouseXYinfo;
        private System.Windows.Forms.Label pixelCountInfo;
        private System.Windows.Forms.Button checkpixelstateBTN;
        private System.Windows.Forms.Label proccessingpixelInfo;
        public System.Windows.Forms.TextBox validationInfo;
        private System.Windows.Forms.Label myUPOInfo;
    }
}

