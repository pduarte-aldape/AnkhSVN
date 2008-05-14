﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Resources;
using WizardFramework;

namespace Ankh.UI.MergeWizard
{
    class MergeSourceReintegratePage : WizardPage
    {
        public MergeSourceReintegratePage() : base("Merge Source Reintegrate")
        {
            IsPageComplete = false;
            Title = resman.GetString("MergeSourceHeaderTitle");
            Message = new WizardMessage(resman.GetString("MergeSourceReintegratePageHeaderMessage"));
        }

        /// <see cref="WizardFramework.IWizardPage.Control" />
        public override System.Windows.Forms.UserControl Control
        {
            get { return control_prop; }
        }

        private System.Windows.Forms.UserControl control_prop = new MergeSourceReintegratePageControl();
        private ResourceManager resman = new ResourceManager("Ankh.UI.MergeWizard.Resources", Assembly.GetExecutingAssembly());
    }
}