# FileAssociationTool
Tool mainly for setting icons and starting applications for music data files
  

[Summary]  
  This tool is mainly used to set icons and startup applications for music data files.  
  It looks at the file name of the prepared ico file and writes the settings for the corresponding file extension in the registry.  
  

[Caution]  
  This tool deletes and updates the related registry such as Explorer to specify the file's icon and startup application.  
  Please back up your computer before executing this tool.  
  Please note that you are responsible for your own actions, and we will not be held responsible for any problems that may occur as a result of this.  
  

[Simple usage]   
  1. First, prepare an .ico file. Name the file as follows  
  　　mdp_???.ico  
     ??? is the file extension of the file to be set.  
     For example, if you want to set .vgm and .xgm, prepare mdp_vgm.ico and mdp_xgm.ico respectively.  

  2. Create an "ico" folder in the folder where FileAssociationTool.exe exists and copy the prepared .ico files into it.  

  3. Run FileAssociationTool.exe. If you have followed steps 1 and 2, the settings will be adjusted accordingly.  
  If not, the default values will be displayed and you can adjust them yourself.  

  4. "Full path (and options) of the program to run",  
  Make sure that the program you want to start when you double-click a file is correctly specified in the "Full path (and options) of the program to run.  
  The default value is "C:\MDPlayer\MDPlayerx64.exe" "%1".  
  (If you have installed MDPlayer as default (without any particular change), this default value should be fine.)  

  5. Click the "Run" button to execute the settings and if successful, the "Success" dialog box will be displayed.  

  6. Click the "X" button to exit.  
  


[description of setting items]  
  [Extension]  
      Enumerates the extensions to be set. Use ";" as a delimiter.  
      For example, to configure .vgm and .xgm, enter ".vgm;.xgm".  
  
  [New Registry Key Prefixes]  
      Sets the name of the registry to add/overwrite to store the detailed settings for each extension.  
      If there is no particular convenience, you can leave the default value as "MDPlayer".  
      For example, if you want to configure .vgm and .xgm settings, registry names "MDPlayer.vgm" and "MDPlayer.xgm" will be added.  
  
  [The full path where the .ico file is located]  
      Specify the full path of the .ico file you want to set.  
      If the full path contains "? is found in the specified full path, it will be replaced with the name of the extension specified in the "Extension" field.  
      For example, "c:\ico\mdp_? .ico" and ".vgm;.xgm" is specified in "extension",  
      c:\ico\mdp_vgm.ico" and "c:\ico\mdp_xgm.ico  
      is specified.  
  
  [The full path (and options) of the program to run]  
      The default value is "C:\MDPlayer/MDPlayerx64.exe" "%1" is entered.  
      If you have installed MDPlayer as default (without any changes), this default value should be fine.  
      If not, please adjust the value.  
      Incidentally, "%1" means the full path of the file you double-clicked.  
      
  
[Copyright and Disclaimer]  
  FileAssociationTool is licensed under the GPLv3 license, see LICENSE.txt.  
  The copyright is owned by the author.  
  This software is provided "as is", without warranty of any kind, and the author assumes no responsibility for any damages resulting from the use of this software.  
  The author assumes no responsibility for any damages resulting from the use of this software.  
  Copyright notice and this permission notice are not required for this software.  


Translated with DeepL.com (free version)    
  
