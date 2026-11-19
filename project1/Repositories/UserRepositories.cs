using project1.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace project1.Repositories
{
    internal class UserRepositories
    {
        private readonly string _filePath = "C:\\Users\\User\\OneDrive\\Desktop\\UA Express\\project1\\Data\\User.txt"
public void SaveUser(User user)
        {
            File.AppendAllLines(_filePath,user)
        }
    }
}
