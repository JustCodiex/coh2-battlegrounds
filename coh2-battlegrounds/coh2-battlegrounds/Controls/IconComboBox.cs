﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace BattlegroundsApp.Controls {
    
    public class IconComboBox : UserControl {

        private class IconComboBoxPopup : Popup {
            private ScrollViewer m_scrollComponent;
            private StackPanel m_itemContainer;
            private Action<int> m_changeEvent;
            public IconComboBoxPopup(Action<int> selectionChange) {
                this.m_changeEvent = selectionChange;
                this.m_scrollComponent = new ScrollViewer();
                this.m_itemContainer = new StackPanel();
                this.m_scrollComponent.Content = this.m_itemContainer;
                this.Child = this.m_scrollComponent;
                this.Width = 160.0;
            }
            public void SetItems(List<IconComboBoxItem> items) {
                this.Clear(); // Clear child elements
                int ctr = 0; // Item counter to keep track of index
                foreach (IconComboBoxItem item in items) {
                    var elem = new IconTextLabel {
                        Icon = item.Icon,
                        Text = item.Text
                    };
                    int i = ctr; // This might seem dumb - but it's due to a lambda capture functionality. Using ctr directly below will not end well :P
                    // A for loop would not inherently fix this (But the solution to the error would be the same)
                    elem.OnLeftMouseButtonClick += (sender, args) => this.m_changeEvent.Invoke(i); // Tell control index is changed
                    this.m_itemContainer.Children.Add(elem); // Add element to list
                    ctr++; // Increment counter
                }
            }
            public void Clear() => this.m_itemContainer.Children.Clear();
        }

        private IconComboBoxPopup m_popup;
        private Image m_imageControl;
        private List<IconComboBoxItem> m_items;

        private int m_selectedIndex;

        public int SelectedIndex { get => this.m_selectedIndex; set => this.SetSelectedIndex(value); }

        public IconComboBoxItem SelectedItem => this.m_items[this.m_selectedIndex];

        public event Action SelectionChanged;

        public IconComboBox() {
            this.m_selectedIndex = -1;
            this.m_imageControl = new Image();
            this.m_imageControl.MouseLeftButtonDown += this.OnLeftMouseButtonImage;
            this.m_popup = new IconComboBoxPopup(i => this.SelectionChangedMethod(i)) {
                Placement = PlacementMode.Bottom,
                PlacementTarget = this.m_imageControl,
                StaysOpen = true
            };
            this.m_popup.LostFocus += this.OnPopupLostFocus;
            this.LostMouseCapture += this.OnLostMouseCapture;
            this.m_items = new List<IconComboBoxItem>();
            this.Content = this.m_imageControl;
        }

        private void OnLostMouseCapture(object sender, MouseEventArgs e) => this.m_popup.IsOpen = false;

        private void OnPopupLostFocus(object sender, RoutedEventArgs e) => this.m_popup.IsOpen = false;

        private void OnLeftMouseButtonImage(object sender, MouseButtonEventArgs e) => this.m_popup.IsOpen = !this.m_popup.IsOpen;

        public void SetItemSource<T>(IEnumerable<T> obj, Func<T, IconComboBoxItem> converter) {
            this.m_items = obj.Select(x => converter(x)).ToList();
            this.m_popup.SetItems(this.m_items);
            this.SetSelectedIndex(0);
        }

        public void Clear() {
            this.m_popup.Clear();
            this.m_items.Clear();
        }

        private void SetSelectedIndex(int index) {
            this.m_imageControl.Source = this.m_items[index].Icon;
            this.m_selectedIndex = index;
            this.SelectionChangedMethod(index, false);
        }

        private void SelectionChangedMethod(int index, bool update = true) {
            if (update) {
                this.SetSelectedIndex(index);
            }
            this.m_popup.IsOpen = false;
            this.SelectionChanged?.Invoke();
        }

    }

}