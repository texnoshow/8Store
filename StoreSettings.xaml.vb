Public NotInheritable Class StoreSettings
    Inherits SettingsFlyout

    Public Sub New()
        Me.InitializeComponent()
        ' Показываем текущий путь, если он есть (это опционально)
    End Sub

    Private Async Sub ChangeFolder_Click(sender As Object, e As RoutedEventArgs)
        Dim picker As New Windows.Storage.Pickers.FolderPicker()
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads
        picker.FileTypeFilter.Add("*")

        Dim folder = Await picker.PickSingleFolderAsync()
        If folder IsNot Nothing Then
            ' Сохраняем новый токен
            Dim token = Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.Add(folder)
            Dim localFolder = Windows.Storage.ApplicationData.Current.LocalFolder
            Dim settingsFile = Await localFolder.CreateFileAsync("folder_config.txt", Windows.Storage.CreationCollisionOption.ReplaceExisting)
            Await Windows.Storage.FileIO.WriteTextAsync(settingsFile, token)

            ' Закрываем панель после выбора
            Me.Hide()

            Dim d As New Windows.UI.Popups.MessageDialog("Папка изменена. Перезапустите приложение для обновления пути.")
            Await d.ShowAsync()
        End If
    End Sub
End Class