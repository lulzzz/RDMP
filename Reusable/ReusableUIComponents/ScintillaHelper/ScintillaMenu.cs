﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using ReusableLibraryCode.Settings;
using ScintillaNET;

namespace ReusableUIComponents.ScintillaHelper
{
    [System.ComponentModel.DesignerCategory("")]
    class ScintillaMenu:ContextMenuStrip
    {
        
        private readonly Scintilla _scintilla;
        private ToolStripMenuItem _miUndo;
        private ToolStripMenuItem _miRedo;
        private ToolStripMenuItem _miCut;
        private ToolStripMenuItem _miCopy;
        private ToolStripMenuItem _miDelete;
        private ToolStripMenuItem _miSelectAll;
        private ToolStripMenuItem _miWordwrap;

        public ScintillaMenu(Scintilla scintilla): base()
        {
            _scintilla = scintilla;
            InitContextMenu();
        }

        private void InitContextMenu()
        {
            this._miUndo = new ToolStripMenuItem("Undo",null, (s, ea) => _scintilla.Undo());
            Items.Add(this._miUndo);
            this._miRedo = new ToolStripMenuItem("Redo", null, (s, ea) => _scintilla.Redo());

            Items.Add(this._miRedo);
            
            Items.Add(new ToolStripSeparator());

            _miWordwrap = new ToolStripMenuItem("Word Wrap");
            foreach (WrapMode mode in Enum.GetValues(typeof(WrapMode)))
            {
                var mi = new ToolStripMenuItem(mode.ToString(), null,SetWordWrapMode){Tag = mode};
                mi.Checked = _scintilla.WrapMode == mode;
                _miWordwrap.DropDownItems.Add(mi);
            }
            Items.Add(_miWordwrap);

            Items.Add(new ToolStripSeparator());

            this._miCut = new ToolStripMenuItem("Cut", null, (s, ea) => _scintilla.Cut());
            Items.Add(_miCut);
            this._miCopy = new ToolStripMenuItem("Copy", null, (s, ea) => _scintilla.Copy());
            Items.Add(_miCopy);
            Items.Add(new ToolStripMenuItem("Paste", null, (s, ea) => _scintilla.Paste()));
            this._miDelete = new ToolStripMenuItem("Delete", null, (s, ea) => _scintilla.ReplaceSelection(""));
            Items.Add(_miDelete);
            Items.Add(new ToolStripSeparator());

            this._miSelectAll = new ToolStripMenuItem("Select All", null, (s, ea) => _scintilla.SelectAll());
            Items.Add(_miSelectAll);
        }

        private void SetWordWrapMode(object sender, EventArgs e)
        {
            var mode = (WrapMode) ((ToolStripMenuItem) sender).Tag;
            UserSettings.WrapMode = (int) mode;
            _scintilla.WrapMode = mode;
        }

        protected override void OnOpening(CancelEventArgs e)
        {
            base.OnOpening(e);

            bool textIsSelected = !string.IsNullOrWhiteSpace(_scintilla.SelectedText);

            _miUndo.Enabled = _scintilla.CanUndo;
            _miRedo.Enabled = _scintilla.CanRedo;
            _miCut.Enabled = textIsSelected;
            _miCopy.Enabled = textIsSelected;
            _miDelete.Enabled = textIsSelected;
            _miSelectAll.Enabled = _scintilla.TextLength > 0;

            //check the current wrap mode and uncheck the rest
            foreach (ToolStripMenuItem item in _miWordwrap.DropDownItems)
                item.Checked = (WrapMode) item.Tag == _scintilla.WrapMode;
        }
    }
}