Imports System.Net
Imports System.IO
Imports System.Runtime.Serialization
Imports System.Runtime.Serialization.Json
Imports System.Drawing
Imports System.Windows.Forms

Public Class Form1
    ' Фирменная палитра
    Private metroColors As Color() = {
        Color.FromArgb(0, 174, 219),
        Color.FromArgb(162, 0, 255),
        Color.FromArgb(240, 150, 9),
        Color.FromArgb(0, 170, 0)
    }
    Private rng As New Random()

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Try
            ServicePointManager.SecurityProtocol = DirectCast(3072, SecurityProtocolType)
        Catch
        End Try

        If Me.DesignMode Then Return

        If flpStore IsNot Nothing Then
            flpStore.AutoScroll = True
            flpStore.WrapContents = False
        End If

        StartLoadingData()
    End Sub

    Private Async Sub StartLoadingData()
        Dim url As String = "https://raw.githubusercontent.com/texnoshow/8Store-apps/refs/heads/main/apps.json"
        Try
            Using client As New WebClient()
                client.Headers.Add("user-agent", "Mozilla/5.0")
                Dim jsonData As Byte() = Await client.DownloadDataTaskAsync(url)

                Using ms As New MemoryStream(jsonData)
                    Dim ser As New DataContractJsonSerializer(GetType(List(Of AppPackage)))
                    Dim apps = DirectCast(ser.ReadObject(ms), List(Of AppPackage))

                    If flpStore IsNot Nothing AndAlso apps IsNot Nothing Then
                        flpStore.Controls.Clear()
                        For Each app In apps
                            CreateTile(app)
                        Next
                    End If
                End Using
            End Using
        Catch ex As Exception
            MessageBox.Show("Ошибка связи с сервером магазина: " & ex.Message)
        End Try
    End Sub

    Private Sub CreateTile(app As AppPackage)
        If flpStore.InvokeRequired Then
            flpStore.Invoke(Sub() CreateTile(app))
            Return
        End If

        Dim pnl As New Panel()
        pnl.Size = New Size(260, 160)
        pnl.BackColor = metroColors(rng.Next(metroColors.Length))
        pnl.Margin = New Padding(10)

        Dim lblTitle As New Label()
        lblTitle.Text = If(String.IsNullOrEmpty(app.Title), "Без названия", app.Title)
        lblTitle.Font = New Font("Segoe UI Semilight", 14)
        lblTitle.ForeColor = Color.White
        lblTitle.Location = New Point(10, 10)
        lblTitle.AutoSize = True
        lblTitle.MaximumSize = New Size(240, 60)

        Dim lblInfo As New Label()
        lblInfo.Text = "Категория: " & app.Category
        lblInfo.Font = New Font("Segoe UI", 9)
        lblInfo.ForeColor = Color.FromArgb(200, 255, 255, 255)
        lblInfo.Location = New Point(10, 75)
        lblInfo.AutoSize = True

        Dim btnInstall As New Button()
        btnInstall.Text = "СКАЧАТЬ"
        btnInstall.Dock = DockStyle.Bottom
        btnInstall.Height = 45
        btnInstall.FlatStyle = FlatStyle.Flat
        btnInstall.BackColor = Color.FromArgb(40, 0, 0, 0)
        btnInstall.ForeColor = Color.White
        btnInstall.Font = New Font("Segoe UI", 10, FontStyle.Bold)
        btnInstall.Tag = app
        btnInstall.FlatAppearance.BorderSize = 0

        AddHandler btnInstall.Click, AddressOf DownloadButton_Click

        pnl.Controls.Add(lblTitle)
        pnl.Controls.Add(lblInfo)
        pnl.Controls.Add(btnInstall)
        flpStore.Controls.Add(pnl)
    End Sub

    Private Sub DownloadButton_Click(sender As Object, e As EventArgs)
        Dim btn = DirectCast(sender, Button)
        Dim app = DirectCast(btn.Tag, AppPackage)

        Dim sfd As New SaveFileDialog()
        sfd.Title = "Сохранение пакета: " & app.Title

        Dim originalFileName As String = Path.GetFileName(New Uri(app.DownloadUrl).AbsolutePath)

        If String.IsNullOrWhiteSpace(originalFileName) Then
            originalFileName = app.Title & ".appx"
        End If

        sfd.FileName = originalFileName
        sfd.Filter = "Все файлы (*.*)|*.*"

        If sfd.ShowDialog() = DialogResult.OK Then
            Try
                Dim wc As New WebClient()

                ' Добавлены senderObj и evArgs для совместимости с VS 2013
                AddHandler wc.DownloadFileCompleted, Sub(senderObj, evArgs)
                                                         MessageBox.Show("Пакет успешно загружен!" & vbCrLf & "Сохранено: " & sfd.FileName, "Загрузка завершена")
                                                         btn.Text = "СКАЧАТЬ"
                                                         btn.Enabled = True
                                                     End Sub

                btn.Text = "ЗАГРУЗКА..."
                btn.Enabled = False

                wc.DownloadFileAsync(New Uri(app.DownloadUrl), sfd.FileName)
            Catch ex As Exception
                MessageBox.Show("Ошибка при скачивании файла: " & ex.Message)
                btn.Enabled = True
            End Try
        End If
    End Sub
End Class

' --- ПОЛНОСТЬЮ УНИФИЦИРОВАННЫЙ КЛАСС ---
<DataContract>
Public Class AppPackage
    <DataMember(Name:="ID")> Public Property ID As String
    <DataMember(Name:="Title")> Public Property Title As String
    <DataMember(Name:="Category")> Public Property Category As String
    <DataMember(Name:="IconPath")> Public Property IconPath As String
    <DataMember(Name:="DownloadUrl")> Public Property DownloadUrl As String
End Class
' <--- УБЕДИСЬ, ЧТО ЭТА СТРОЧКА END CLASS СКОПИРОВАЛАСЬ И ОНА ПОСЛЕДНЯЯ В ФАЙЛЕ