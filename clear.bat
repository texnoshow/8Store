@echo off
echo Закрой Visual Studio перед продолжением!
pause

echo Удаление временных файлов сборки...
rd /s /q "bin"
rd /s /q "obj"

echo Очистка кэша пользователя...
del /q /f /s *.suo
del /q /f /s *.user

echo Поиск и удаление папки .vs (если есть)...
if exist ".vs" rd /s /q ".vs"

echo.
echo Все временные файлы удалены! 
echo Теперь открывай проект и делай Rebuild Solution.
pause