Imports System.IO
Imports System.Text
Imports System.Collections.ObjectModel
Imports System.Runtime.Serialization.Json
Imports Windows.UI.Popups
Imports Windows.UI.Xaml
Imports Windows.UI.Xaml.Controls
Imports Windows.UI.Xaml.Navigation
Imports Windows.Storage
Imports Windows.Storage.Pickers
Imports Windows.Web.Http
Imports Windows.Storage.AccessCache
Imports Windows.UI.ApplicationSettings

' Классы данных
<Runtime.Serialization.DataContract>
Public Class AppPackage
    <Runtime.Serialization.DataMember(Name:="ID")> Public Property ID As String
    <Runtime.Serialization.DataMember(Name:="Title")> Public Property Name As String
    <Runtime.Serialization.DataMember(Name:="Category")> Public Property Category As String
    <Runtime.Serialization.DataMember(Name:="DownloadUrl")> Public Property DownloadUrl As String
    Public Property IconUrl As String = "ms-appx:///Assets/Logo.scale-100.png"
End Class

Public Class StoreViewModel
    Public Property StaffPicks As New ObservableCollection(Of AppPackage)()
End Class

Partial Public NotInheritable Class MainPage
    Inherits Page

    Private _viewModel As New StoreViewModel()
    Private _targetFolder As StorageFolder = Nothing
    Private Const SETTINGS_FILE As String = "folder_config.txt"

    Public Sub New()
        Me.InitializeComponent()
        Me.DataContext = _viewModel
    End Sub

    Private Function CheckIsWindows10() As Boolean
        Try
            Dim typeName = "Windows.UI.ViewManagement.ApplicationViewTitleBar, Windows, ContentType=WindowsRuntime"
            Return Type.GetType(typeName) IsNot Nothing
        Catch
            Return False
        End Try
    End Function

    Protected Overrides Async Sub OnNavigatedTo(e As NavigationEventArgs)
        Dim isWin10 As Boolean = CheckIsWindows10()

        If isWin10 Then
            PickFolderButton.Visibility = Visibility.Visible
        Else
            PickFolderButton.Visibility = Visibility.Collapsed
            Try
                AddHandler SettingsPane.GetForCurrentView().CommandsRequested, AddressOf OnCommandsRequested
            Catch
            End Try
        End If

        Await InitializeFolderSettings()
        Await LoadStoreData()
    End Sub

    Private Sub OnCommandsRequested(sender As SettingsPane, args As SettingsPaneCommandsRequestedEventArgs)
        Dim cmd As New SettingsCommand("folderSettings", "Папка загрузок",
            Sub(handler)
                ShowSettingsFlyout()
            End Sub)
        args.Request.ApplicationCommands.Add(cmd)
    End Sub

    Private Sub ShowSettingsFlyout()
        Try
            Dim flyout As New StoreSettings()
            flyout.Show()
        Catch
            ' Здесь мы вызываем асинхронный метод без Await, 
            ' так как это событие клика/команды (Fire and Forget)
            Dim task = PerformFolderPicking()
        End Try
    End Sub

    Private Async Function InitializeFolderSettings() As System.Threading.Tasks.Task
        Dim needSetup As Boolean = False
        Try
            Dim localFolder As StorageFolder = ApplicationData.Current.LocalFolder
            Dim item = Await localFolder.TryGetItemAsync(SETTINGS_FILE)

            If item IsNot Nothing Then
                Dim file As StorageFile = DirectCast(item, StorageFile)
                Dim folderToken As String = Await FileIO.ReadTextAsync(file)

                If StorageApplicationPermissions.FutureAccessList.ContainsItem(folderToken) Then
                    _targetFolder = Await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(folderToken)
                Else
                    needSetup = True
                End If
            Else
                needSetup = True
            End If
        Catch
            needSetup = True
        End Try

        ' Выносим логику настройки ПРЕДУПРЕЖДЕНИЯ из блока Try
        If needSetup Then
            Await SetupNewFolder()
        End If
    End Function

    Private Async Function SetupNewFolder() As System.Threading.Tasks.Task
        PickFolderButton.Visibility = Visibility.Visible
        Dim d As New MessageDialog("Пожалуйста, выберите папку для сохранения приложений.")
        Await d.ShowAsync()
        Await PerformFolderPicking()
    End Function

    Private Async Function PerformFolderPicking() As System.Threading.Tasks.Task
        Dim picker As New FolderPicker()
        picker.SuggestedStartLocation = PickerLocationId.Downloads
        picker.FileTypeFilter.Add("*")
        Dim folder As StorageFolder = Await picker.PickSingleFolderAsync()

        If folder IsNot Nothing Then
            _targetFolder = folder
            Dim token As String = StorageApplicationPermissions.FutureAccessList.Add(folder)
            Dim localFolder As StorageFolder = ApplicationData.Current.LocalFolder
            Dim settingsFile As StorageFile = Await localFolder.CreateFileAsync(SETTINGS_FILE, CreationCollisionOption.ReplaceExisting)
            Await FileIO.WriteTextAsync(settingsFile, token)

            If Not CheckIsWindows10() Then PickFolderButton.Visibility = Visibility.Collapsed
        End If
    End Function

    Public Sub PickFolderButton_Click(sender As Object, e As RoutedEventArgs)
        ShowSettingsFlyout()
    End Sub

    Private Async Function LoadStoreData() As System.Threading.Tasks.Task
        LoadingBar.Visibility = Visibility.Visible
        Try
            Using client As New HttpClient()
                Dim uri As New Uri("https://cdn.jsdelivr.net/gh/texnoshow/8Store-apps@main/apps.json?t=" & DateTime.Now.Ticks.ToString())
                Dim response As String = Await client.GetStringAsync(uri)
                Using ms As New MemoryStream(Encoding.UTF8.GetBytes(response))
                    Dim ser As New DataContractJsonSerializer(GetType(List(Of AppPackage)))
                    Dim apps = DirectCast(ser.ReadObject(ms), List(Of AppPackage))
                    _viewModel.StaffPicks.Clear()
                    For Each a In apps
                        If a IsNot Nothing Then _viewModel.StaffPicks.Add(a)
                    Next
                End Using
            End Using
        Catch
        End Try
        LoadingBar.Visibility = Visibility.Collapsed
    End Function

    Public Async Sub ListView_ItemClick(sender As Object, e As ItemClickEventArgs)
        Dim app = TryCast(e.ClickedItem, AppPackage)
        If app Is Nothing OrElse _targetFolder Is Nothing Then Return

        Dim statusMessage As String = ""
        LoadingBar.Visibility = Visibility.Visible

        Try
            Dim fileName As String = app.Name.Replace(" ", "_") & ".appx"
            Dim file As StorageFile = Await _targetFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName)
            Dim downloader As New Windows.Networking.BackgroundTransfer.BackgroundDownloader()
            Dim download = downloader.CreateDownload(New Uri(app.DownloadUrl), file)

            Await download.StartAsync()
            statusMessage = "Скачивание завершено: " & app.Name
        Catch ex As Exception
            statusMessage = "Ошибка: " & ex.Message
        End Try

        LoadingBar.Visibility = Visibility.Collapsed

        ' ВЫВОДИМ СООБЩЕНИЕ ЗДЕСЬ (ВНЕ БЛОКА CATCH)
        If Not String.IsNullOrEmpty(statusMessage) Then
            Dim d As New MessageDialog(statusMessage)
            Await d.ShowAsync()
        End If
    End Sub
End Class