Imports System
Imports Windows.ApplicationModel
Imports Windows.ApplicationModel.Activation
Imports Windows.UI.Xaml
Imports Windows.UI.Xaml.Controls

Public NotInheritable Class App
    Inherits Application

    Protected Overrides Sub OnLaunched(args As LaunchActivatedEventArgs)
        Dim rootFrame As Frame = Window.Current.Content
        If rootFrame Is Nothing Then
            rootFrame = New Frame()
            Window.Current.Content = rootFrame
        End If

        If rootFrame.Content Is Nothing Then
            rootFrame.Navigate(GetType(MainPage), args.Arguments)
        End If
        Window.Current.Activate()
    End Sub
End Class