﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BattlegroundsApp.Dialogs.Service;
using BattlegroundsApp.Utilities;
using BattlegroundsApp.ViewModels;

namespace BattlegroundsApp.Dialogs.ImportExport {
    public enum ImportExportDialogType {
        Import,
        Export
    }
    public enum ImportExportDialogResult {
        Import,
        Cancel
    }
    class ImportExportDialogViewModel : DialogWindowBase<ImportExportDialogResult> {

        public ICommand ImportExportCommand { get; set; }
        public ICommand CancelCommand { get; set; }
        public string ButtonName { get; set; }
        public ImportExportDialogType Type { get; set; }

        private string _companyString;
        public string CompanyString { 
            get {
                return _companyString;
            }

            set {
                this._companyString = value;
                OnPropertyChanged(nameof(CompanyString));
            }
        }

        private ImportExportDialogViewModel(ImportExportDialogType type, string title) {

            Title = title;
            CancelCommand = new RelayCommand<DialogWindow>(Cancel);
            ButtonName = (type == ImportExportDialogType.Import) ? "Import" : "Copy";
            ImportExportCommand = (type == ImportExportDialogType.Import) ? new RelayCommand<DialogWindow>(Import) : new RelayCommand<DialogWindow>(Export);
            Type = (type == ImportExportDialogType.Import) ? ImportExportDialogType.Import : ImportExportDialogType.Export;
            DialogCloseDefault = ImportExportDialogResult.Cancel;

        }

        private ImportExportDialogViewModel(ImportExportDialogType type, string title, string companyString) : this(type, title) { this.CompanyString = companyString; }

        public static ImportExportDialogResult ShowImportDialog(string title) {
            var dialog = new ImportExportDialogViewModel(ImportExportDialogType.Import, title);
            return dialog.ShowDialog();
        }
        public static ImportExportDialogResult ShowExportDialog(string title, string companyString) {
            var dialog = new ImportExportDialogViewModel(ImportExportDialogType.Export, title, companyString);
            return dialog.ShowDialog();
        }

        private void Import(DialogWindow window) {

            CloseDialogWithResult(window, ImportExportDialogResult.Import);
        }

        private void Export(DialogWindow window) {

            Clipboard.SetData(DataFormats.Text, CompanyString);

        }

        private void Cancel(DialogWindow window) {

            CloseDialogWithResult(window, ImportExportDialogResult.Cancel);

        }
    }
}
