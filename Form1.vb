Imports System.Net
Imports System.IO
Imports System.Runtime.Serialization
Imports System.Runtime.Serialization.Json
Imports System.Drawing
Imports System.Windows.Forms
Imports System.Diagnostics
Imports System.Linq
Imports System.Runtime.InteropServices

Public Class Form1
    ' --- ГЛОБАЛЬНЫЕ ДАННЫЕ ---
    Private allApps As New List(Of AppPackage)()
    Private flpCategories As FlowLayoutPanel
    Private txtSearch As TextBox
    Private currentCategory As String = "Все" ' Запоминаем текущую категорию

    Private metroColors As Color() = {
        Color.FromArgb(0, 174, 219), Color.FromArgb(162, 0, 255),
        Color.FromArgb(240, 150, 9), Color.FromArgb(0, 170, 0)
    }
    Private rng As New Random()

    <DllImport("user32.dll")> Public Shared Function ReleaseCapture() As Boolean
    End Function
    <DllImport("user32.dll")> Public Shared Function SendMessage(ByVal hWnd As IntPtr, ByVal Msg As Integer, ByVal wParam As Integer, ByVal lParam As Integer) As Integer
    End Function

    ' --- АЛГОРИТМ ЛЕВЕНШТЕЙНА (УМНЫЙ ПОИСК С ОПЕЧАТКАМИ ЗА 15 СТРОК) ---
    Private Function GetLevenshteinDistance(s As String, t As String) As Integer
        If String.IsNullOrEmpty(s) Then Return If(t Is Nothing, 0, t.Length)
        If String.IsNullOrEmpty(t) Then Return s.Length
        Dim d(s.Length, t.Length) As Integer
        For i = 0 To s.Length : d(i, 0) = i : Next
        For j = 0 To t.Length : d(0, j) = j : Next
        For i = 1 To s.Length
            For j = 1 To t.Length
                Dim cost = If(s(i - 1) = t(j - 1), 0, 1)
                d(i, j) = Math.Min(Math.Min(d(i - 1, j) + 1, d(i, j - 1) + 1), d(i - 1, j - 1) + cost)
            Next
        Next
        Return d(s.Length, t.Length)
    End Function
    ' -------------------------------------------------------------------

    Private Function GetSystemVersion() As Version
        Try
            Using key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows NT\CurrentVersion")
                If key IsNot Nothing Then
                    Dim major = key.GetValue("CurrentMajorVersionNumber")
                    If major IsNot Nothing Then Return New Version(CInt(major), CInt(key.GetValue("CurrentMinorVersionNumber", 0)))
                    Dim versionStr = TryCast(key.GetValue("CurrentVersion"), String)
                    If Not String.IsNullOrEmpty(versionStr) Then Return New Version(versionStr)
                End If
            End Using
        Catch
        End Try
        Return Environment.OSVersion.Version
    End Function

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Try : ServicePointManager.SecurityProtocol = DirectCast(3072, SecurityProtocolType) : Catch : End Try
        If Me.DesignMode Then Return

        Me.FormBorderStyle = FormBorderStyle.None
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.Size = New Size(1000, 600)
        Me.BackColor = Color.FromArgb(20, 20, 20)
        Me.Padding = New Padding(1)

        ' --- ШАПКА ОКНА ---
        Dim pnlTitleBar As New Panel With {.Height = 45, .Dock = DockStyle.Top, .BackColor = Color.FromArgb(30, 30, 30)}
        Me.Controls.Add(pnlTitleBar)

        Dim lblAppTitle As New Label With {.Text = "8STORE", .ForeColor = Color.White, .Font = New Font("Segoe UI Semibold", 12), .Location = New Point(15, 10), .AutoSize = True}
        pnlTitleBar.Controls.Add(lblAppTitle)

        ' --- КНОПКИ УПРАВЛЕНИЯ (ДОБАВЛЯЕМ В ПРАВЫЙ УГОЛ) ---
        Dim btnClose As New Button With {.Text = "✕", .Width = 45, .Dock = DockStyle.Right, .FlatStyle = FlatStyle.Flat, .ForeColor = Color.White, .Font = New Font("Segoe UI", 10)}
        btnClose.FlatAppearance.BorderSize = 0
        AddHandler btnClose.Click, Sub() Me.Close()
        AddHandler btnClose.MouseEnter, Sub() btnClose.BackColor = Color.FromArgb(232, 17, 35)
        AddHandler btnClose.MouseLeave, Sub() btnClose.BackColor = pnlTitleBar.BackColor

        Dim btnMax As New Button With {.Text = ChrW(&H25A1), .Width = 45, .Dock = DockStyle.Right, .FlatStyle = FlatStyle.Flat, .ForeColor = Color.White, .Font = New Font("Segoe UI", 12)}
        btnMax.FlatAppearance.BorderSize = 0
        AddHandler btnMax.Click, Sub() Me.WindowState = If(Me.WindowState = FormWindowState.Normal, FormWindowState.Maximized, FormWindowState.Normal)

        Dim btnMin As New Button With {.Text = "—", .Width = 45, .Dock = DockStyle.Right, .FlatStyle = FlatStyle.Flat, .ForeColor = Color.White, .Font = New Font("Segoe UI", 10, FontStyle.Bold)}
        btnMin.FlatAppearance.BorderSize = 0
        AddHandler btnMin.Click, Sub() Me.WindowState = FormWindowState.Minimized

        ' --- ПОИСК В ШАПКЕ ОКНА (РЯДОМ С КНОПКАМИ) ---
        Dim pnlSearch As New Panel With {.Width = 250, .Dock = DockStyle.Right, .Padding = New Padding(10, 10, 20, 10)}
        txtSearch = New TextBox With {.Dock = DockStyle.Fill, .BackColor = Color.FromArgb(45, 45, 45), .ForeColor = Color.White, .BorderStyle = BorderStyle.FixedSingle, .Font = New Font("Segoe UI", 10)}

        ' Подсказка внутри поля поиска
        txtSearch.Text = "Поиск..."
        txtSearch.ForeColor = Color.Gray
        AddHandler txtSearch.GotFocus, Sub()
                                           If txtSearch.Text = "Поиск..." Then txtSearch.Text = "" : txtSearch.ForeColor = Color.White
                                       End Sub
        AddHandler txtSearch.LostFocus, Sub()
                                            If String.IsNullOrWhiteSpace(txtSearch.Text) Then txtSearch.Text = "Поиск..." : txtSearch.ForeColor = Color.Gray
                                        End Sub

        ' Запуск поиска при вводе
        AddHandler txtSearch.TextChanged, Sub()
                                              If txtSearch.Text <> "Поиск..." Then ShowApps(currentCategory)
                                          End Sub

        pnlSearch.Controls.Add(txtSearch)

        ' Порядок добавления влияет на расположение DockStyle.Right (справа налево)
        pnlTitleBar.Controls.Add(btnClose)
        pnlTitleBar.Controls.Add(btnMax)
        pnlTitleBar.Controls.Add(btnMin)
        pnlTitleBar.Controls.Add(pnlSearch) ' Поиск встанет ровно слева от кнопки Свернуть

        ' Перетаскивание за шапку
        Dim DragWindow As MouseEventHandler = Sub(s, ev)
                                                  If ev.Button = MouseButtons.Left Then
                                                      ReleaseCapture()
                                                      SendMessage(Me.Handle, &HA1, &H2, 0)
                                                  End If
                                              End Sub
        AddHandler pnlTitleBar.MouseDown, DragWindow
        AddHandler lblAppTitle.MouseDown, DragWindow

        ' --- БОКОВОЕ МЕНЮ И ОСНОВНАЯ ПАНЕЛЬ ---
        flpCategories = New FlowLayoutPanel With {.Dock = DockStyle.Left, .Width = 200, .BackColor = Color.FromArgb(30, 30, 30), .FlowDirection = FlowDirection.TopDown, .WrapContents = False}
        Me.Controls.Add(flpCategories)

        If flpStore IsNot Nothing Then
            flpStore.Dock = DockStyle.Fill
            flpStore.BackColor = Color.FromArgb(20, 20, 20)
            flpStore.BringToFront()
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
                    allApps = DirectCast(ser.ReadObject(ms), List(Of AppPackage))
                    BuildCategoryMenu()
                    ShowApps("Все")
                End Using
            End Using
        Catch ex As Exception
            MessageBox.Show("Ошибка сети: " & ex.Message)
        End Try
    End Sub

    Private Sub BuildCategoryMenu()
        flpCategories.Controls.Clear()

        Dim lblMenu As New Label With {.Text = "Категории", .Font = New Font("Segoe UI Semilight", 16), .ForeColor = Color.White, .Margin = New Padding(15, 20, 10, 15), .AutoSize = True}
        flpCategories.Controls.Add(lblMenu)

        AddCategoryButton("Все")
        Dim cats = allApps.Select(Function(a) a.Category).Where(Function(c) Not String.IsNullOrWhiteSpace(c)).Distinct().OrderBy(Function(c) c).ToList()
        For Each c In cats
            AddCategoryButton(c)
        Next
    End Sub

    Private Sub AddCategoryButton(catName As String)
        Dim btn As New Button With {.Text = catName, .Width = 200, .Height = 45, .FlatStyle = FlatStyle.Flat, .ForeColor = Color.LightGray, .BackColor = Color.FromArgb(30, 30, 30), .Font = New Font("Segoe UI", 11), .Cursor = Cursors.Hand, .TextAlign = ContentAlignment.MiddleLeft, .Padding = New Padding(15, 0, 0, 0)}
        btn.FlatAppearance.BorderSize = 0

        AddHandler btn.MouseEnter, Sub() btn.BackColor = Color.FromArgb(50, 50, 50)
        AddHandler btn.MouseLeave, Sub() btn.BackColor = Color.FromArgb(30, 30, 30)
        AddHandler btn.Click, Sub()
                                  currentCategory = catName
                                  ShowApps(currentCategory)
                              End Sub
        flpCategories.Controls.Add(btn)
    End Sub

    ' --- ЛОГИКА ФИЛЬТРАЦИИ И ПОИСКА ---
    Private Sub ShowApps(selectedCat As String)
        flpStore.Controls.Clear()
        Dim query = txtSearch.Text.ToLower().Trim()
        If query = "поиск..." Then query = ""

        Dim appsToShow As New List(Of AppPackage)

        For Each app In allApps
            ' 1. Проверяем категорию
            Dim matchCategory = (selectedCat = "Все" OrElse app.Category = selectedCat)

            ' 2. Проверяем поиск
            Dim matchSearch As Boolean = True
            If Not String.IsNullOrEmpty(query) Then
                Dim titleLower = app.Title.ToLower()
                ' Точное совпадение или допускаем до 2-х опечаток (расстояние Левенштейна <= 2)
                matchSearch = titleLower.Contains(query) OrElse GetLevenshteinDistance(titleLower, query) <= 2
            End If

            ' Если совпадает и то, и другое - показываем
            If matchCategory AndAlso matchSearch Then
                appsToShow.Add(app)
            End If
        Next

        For Each app In appsToShow
            CreateTile(app)
        Next
    End Sub

    Private Sub CreateTile(app As AppPackage)
        Dim pnl As New Panel With {.Size = New Size(260, 160), .BackColor = metroColors(rng.Next(metroColors.Length)), .Margin = New Padding(15)}

        Dim lblTitle As New Label With {.Text = If(String.IsNullOrEmpty(app.Title), "Без названия", app.Title), .Font = New Font("Segoe UI Semilight", 14), .ForeColor = Color.White, .Location = New Point(10, 10), .AutoSize = True, .MaximumSize = New Size(240, 60)}

        Dim osReq As String = If(String.IsNullOrEmpty(app.MinOSVersion), "6.2", app.MinOSVersion)
        osReq &= If(Not String.IsNullOrEmpty(app.MaxOSVersion), " - " & app.MaxOSVersion, "+")

        Dim lblInfo As New Label With {.Text = "Win " & osReq, .Font = New Font("Segoe UI", 9), .ForeColor = Color.FromArgb(200, 255, 255, 255), .Location = New Point(10, 75), .AutoSize = True}

        Dim btnInstall As New Button With {.Text = "СКАЧАТЬ", .Dock = DockStyle.Bottom, .Height = 45, .FlatStyle = FlatStyle.Flat, .BackColor = Color.FromArgb(40, 0, 0, 0), .ForeColor = Color.White, .Font = New Font("Segoe UI", 10, FontStyle.Bold), .Tag = app}
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
        Dim currentOS As Version = GetSystemVersion()

        If currentOS.Major = 6 AndAlso (currentOS.Minor = 2 OrElse currentOS.Minor = 3) Then
            If Not String.IsNullOrEmpty(app.MinOSVersion) Then
                Dim minReq As Version
                If Version.TryParse(app.MinOSVersion, minReq) AndAlso currentOS < minReq Then
                    MessageBox.Show("Требуется минимум Win " & app.MinOSVersion, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error)
                    Return
                End If
            End If
            If Not String.IsNullOrEmpty(app.MaxOSVersion) Then
                Dim maxReq As Version
                If Version.TryParse(app.MaxOSVersion, maxReq) AndAlso currentOS > maxReq Then
                    MessageBox.Show("Максимально поддерживаемая: " & app.MaxOSVersion, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error)
                    Return
                End If
            End If
        End If

        Dim tempFilePath As String = Path.Combine(Path.GetTempPath(), If(String.IsNullOrWhiteSpace(Path.GetFileName(New Uri(app.DownloadUrl).AbsolutePath)), app.Title & ".appx", Path.GetFileName(New Uri(app.DownloadUrl).AbsolutePath)))
        btn.Enabled = False : btn.Text = "ПОДГОТОВКА..."

        Try
            Dim wc As New WebClient()
            AddHandler wc.DownloadProgressChanged, Sub(sObj, evArgs) btn.Text = "ЗАГРУЗКА: " & evArgs.ProgressPercentage & "%"
            AddHandler wc.DownloadFileCompleted, Sub(sObj, evArgs)
                                                     If evArgs.Error IsNot Nothing Then
                                                         MessageBox.Show("Ошибка: " & evArgs.Error.Message)
                                                         btn.Text = "СКАЧАТЬ" : btn.Enabled = True : Return
                                                     End If
                                                     btn.Text = "УСТАНОВКА..."
                                                     Try
                                                         Dim psi As New ProcessStartInfo With {.FileName = "powershell.exe", .Arguments = "-ExecutionPolicy Bypass -NoProfile -WindowStyle Hidden -Command ""try { Add-AppxPackage -Path '" & tempFilePath & "' -ErrorAction Stop } catch { exit 1 }""", .CreateNoWindow = True, .UseShellExecute = False}
                                                         Dim process As Process = Process.Start(psi)
                                                         process.WaitForExit()
                                                         If process.ExitCode = 0 Then
                                                             MessageBox.Show("Приложение установлено!")
                                                             Try : If File.Exists(tempFilePath) Then File.Delete(tempFilePath)
                                                             Catch : End Try
                                                         Else
                                                             MessageBox.Show("Отсутствует сертификат. Установите его из свойств файла.")
                                                             Try : Process.Start("explorer.exe", "/select,""" & tempFilePath & """") : Catch : End Try
                                                         End If
                                                     Catch exProcess As Exception
                                                         MessageBox.Show("Ошибка: " & exProcess.Message)
                                                     Finally
                                                         btn.Text = "СКАЧАТЬ" : btn.Enabled = True
                                                     End Try
                                                 End Sub
            wc.DownloadFileAsync(New Uri(app.DownloadUrl), tempFilePath)
        Catch ex As Exception
            MessageBox.Show("Ошибка: " & ex.Message)
            btn.Enabled = True : btn.Text = "СКАЧАТЬ"
        End Try
    End Sub
End Class

<DataContract>
Public Class AppPackage
    <DataMember(Name:="ID")> Public Property ID As String
    <DataMember(Name:="Title")> Public Property Title As String
    <DataMember(Name:="Category")> Public Property Category As String
    <DataMember(Name:="IconPath")> Public Property IconPath As String
    <DataMember(Name:="DownloadUrl")> Public Property DownloadUrl As String
    <DataMember(Name:="MinOSVersion")> Public Property MinOSVersion As String
    <DataMember(Name:="MaxOSVersion")> Public Property MaxOSVersion As String
End Class