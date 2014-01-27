﻿using ImoutoViewer.Behavior;
using ImoutoViewer.Commands;
using ImoutoViewer.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ImoutoViewer.ViewModel
{
    class MainWindowVM : VMBase, IDragable, IDropable
    {
        #region Fields

        private MainWindow _mainWindowView;
        private LocalImageList _imageList;
        private SettingsVM _settings;
        private BackgroundWorker _initBackgroundWorker;
        private BackgroundWorker _navigateBackgroundWorker;
        
        #endregion //Fields
        
        #region Constructors

        public MainWindowVM()
        {
            IsSimpleWheelNavigationEnable = true;            

            _mainWindowView = new MainWindow();
            _mainWindowView.DataContext = this;
            _mainWindowView.SizeChanged += _mainWindowView_SizeChanged;
            InitializeCommands();
            InitializeSettings();
            _mainWindowView.Show();

            _initBackgroundWorker = new BackgroundWorker();
            _initBackgroundWorker.RunWorkerCompleted += _backgroundWorker_RunWorkerCompleted;
            _initBackgroundWorker.DoWork += _backgroundWorker_DoWork;
            _initBackgroundWorker.RunWorkerAsync();


        }

        void _backgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            IsLoading = true;
            OnPropertyChanged("IsLoading");

            if ((e.Argument as string[]) != null)
            {
                InitializeImageList(e.Argument as string[]);
            }
            else
            {
                InitializeImageList();
            }
        }

        private void _backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            IsLoading = false;
            OnPropertyChanged("IsLoading");
            UpdateView();
        }

        #endregion //Constructors

        #region Properties

        private LocalImage CurrentLocalImage
        {
            get
            {
                if (IsError)
                {
                    return null;
                }
                else
                {
                    BitmapSource bs = _imageList.CurrentImage.Image;
                    if (!IsError)
                    {
                        _imageList.CurrentImage.Resize(_mainWindowView.Client.RenderSize, Settings.SelectedResizeType.Type);
                        return _imageList.CurrentImage;
                    }
                    else
                    {
                        return null;
                    }                
                }
            }
        }

        public string Title
        {
            get
            {
                if (IsError)
                {
                    return "Image loading error";
                } 
                else if (IsLoading)
                {
                    return "Loading...";
                }
                else
                {
                    return String.Format("Dir {3} / {4} : {5} | File {1} / {2} : {0}",
                        CurrentLocalImage.Name,
                        _imageList.CurrentImageIndex + 1,
                        _imageList.ImagesCount,
                        _imageList.CurrentDirectoryIndex + 1,
                        _imageList.DirectoriesCount,
                        _imageList.CurrentDirectory.Name);
                }
                
            }
        }

        public BitmapSource Image
        {
            get
            {

                if (CurrentLocalImage == null || CurrentLocalImage.ImageFormat == ImageFormat.GIF)
                {
                    return null;
                }
                else
                {
                    return CurrentLocalImage.Image;
                }
            }
        }

        public BitmapSource AnimutedImage
        {
            get
            {
                if (CurrentLocalImage != null && CurrentLocalImage.ImageFormat == ImageFormat.GIF )
                {
                    return CurrentLocalImage.Image;
                }
                else
                {
                    return null;
                }
            }
        }

        public bool IsAnimuted
        {
            get
            {
                if (CurrentLocalImage == null) return false;

                return (CurrentLocalImage.ImageFormat == ImageFormat.GIF);
            }
        }

        public bool IsLoading { get; private set; }

        public double ViewportHeight
        {
            get
            {
                if (CurrentLocalImage == null) return 0;

                return CurrentLocalImage.ResizedSize.Height;
            }
        }

        public double ViewportWidth
        {
            get
            {
                if (CurrentLocalImage == null) return 0;

                return CurrentLocalImage.ResizedSize.Width;
            }
        }

        public bool IsSimpleWheelNavigationEnable { get; set; }

        public SettingsVM Settings
        {
            get
            {
                return _settings;
            }
        }

        public bool IsError
        {
            get
            {
                return _imageList.CurrentImage.IsError;
            }
        }

        public string ErrorMessage
        {
            get
            {
                return _imageList.CurrentImage.ErrorMessage;
            }
        }

        #endregion //Properties

        #region Methods

        private void InitializeSettings()
        {
            _settings = new SettingsVM();
            _settings.SelectedResizeTypeChanged += _settings_SelectedResizeTypeChanged;
            _settings.SelectedDirectorySearchTypeChanged += _settings_SelectedDirectorySearchTypeChanged;
        }

        private void InitializeImageList(string[] images = null)
        {
            if (images != null)
            {
                _imageList = new LocalImageList(images, Settings.DirectorySearchFlags);
            }
            else if (Application.Current.Properties["ArbitraryArgName"] != null)
            {
                string fname = Application.Current.Properties["ArbitraryArgName"].ToString();
                Application.Current.Properties["ArbitraryArgName"] = null;

                _imageList = new LocalImageList(fname, Settings.DirectorySearchFlags);
            }
            #if DEBUG
            else
            {
                _imageList = new LocalImageList(@"c:\Users\oniii-chan\Downloads\DLS\art\loli\715e2f290f6c236fdd6426d83ab9a9e0.jpg", Settings.DirectorySearchFlags);
            }
            #else
            else
            {
                _imageList = new LocalImageList();
            }            
            #endif
        }

        private void UpdateView()
        {
            try
            {
                OnPropertyChanged("Title");
                OnPropertyChanged("ViewportHeight");
                OnPropertyChanged("ViewportWidth");
                OnPropertyChanged("AnimutedImage");
                OnPropertyChanged("Image");
                OnPropertyChanged("IsAnimuted");
                OnPropertyChanged("IsError");
                OnPropertyChanged("ErrorMessage");
            }
            catch (OutOfMemoryException e)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();

                UpdateView();
            }
            catch
            {
                NextImage();
            }
        }

        #endregion //Methods

        #region Commands

        public ICommand SimpleNextImageCommand { get; private set; }
        public ICommand SimplePrevImageCommand { get; private set; }
        public ICommand NextImageCommand { get; private set; }
        public ICommand PrevImageCommand { get; private set; }
        public ICommand ZoomInCommand { get; private set; }
        public ICommand ZoomOutCommand { get; private set; }

        /// <summary>
        /// Rotation the image. In CommandParameter as string send: 
        /// "right" to rotate image on 90 deg right; 
        /// "left to rotate image on -90 deg left.
        /// </summary>
        public ICommand RotateCommand { get; private set; }

        private void InitializeCommands()
        {
            SimpleNextImageCommand = new RelayCommand(param => 
                {
                    if (IsSimpleWheelNavigationEnable)
                    {
                        NextImage();
                    }
                });

            SimplePrevImageCommand = new RelayCommand(param =>
                {
                    if (IsSimpleWheelNavigationEnable)
                    {
                        PrevImage();
                    }
                });

            NextImageCommand = new RelayCommand(param => NextImage());
            PrevImageCommand = new RelayCommand(param => PrevImage());

            ZoomInCommand = new RelayCommand(param => ZoomIn());
            ZoomOutCommand = new RelayCommand(param => ZoomOut());

            RotateCommand = new RelayCommand(param => Rotate(param));
        }

        #endregion //Commands

        #region Command handlers

        private void NextImage()
        {            
            _imageList.Next();
            UpdateView();
        }

        private void PrevImage()
        {
            _imageList.Previous();
            UpdateView();
        }

        private void ZoomIn()
        {
            CurrentLocalImage.ZoomIn();
            UpdateView();
        }

        private void ZoomOut()
        {
            CurrentLocalImage.ZoomOut();
            UpdateView();
        }

        private void Rotate(object param)
        {
            switch (param as string)
            {
                case "left":
                    CurrentLocalImage.RotateLeft();
                    break;
                case "right":
                    CurrentLocalImage.RotateRight();
                    break;
                default:
                    break;
            }
            UpdateView();
        }

        #endregion //Command handlers

        #region Event handlers

        private void _mainWindowView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateView(); ///???
        }

        void _settings_SelectedResizeTypeChanged(object sender, EventArgs e)
        {
            UpdateView();
        }

        void _settings_SelectedDirectorySearchTypeChanged(object sender, EventArgs e)
        {
            _initBackgroundWorker.RunWorkerAsync(new string[] { CurrentLocalImage.Path });
        }

        #endregion //Event handlers

        #region IDragable members

        public string DataType
        { 
            get 
            {
                return DataFormats.FileDrop; 
            } 
        }

        public object Data 
        {
            get
            {
                return new DataObject(DataFormats.FileDrop, new string[] { CurrentLocalImage.Path });
            }
        }

        public DragDropEffects AllowDragDropEffects
        {
            get
            {
                return DragDropEffects.Copy;
            }
        }

        #endregion //IDragable members

        #region IDropable members

        public List<string> AllowDataTypes
        {
            get
            {
                List<string> list = new List<string>();
                list.Add(DataFormats.FileDrop);

                return list;
            }
        }

        public void Drop(object data)
        {
            string[] droppedFiles = (string[])data;

            _initBackgroundWorker.RunWorkerAsync(droppedFiles);
        }

        #endregion //IDropable members
    }
}
