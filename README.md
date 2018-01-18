# Windbg extension for R

This dll allows you to run R commands inside Windbg.  
R scripting has a rich model to parse data and do statistics & machine learning.  
This can be useful to clean windbg ouputs and get some statistics.

# Install current release of the R extension for windbg

R needs to be installed on the machine in order to use it from windbg.  
You can download and install R from  https://cran.r-project.org/mirrors.html.  
It works as well with Microsoft R Client https://docs.microsoft.com/en-us/machine-learning-server/r-client/what-is-microsoft-r-client which adds additianal commands in R.

Unzip the current release from https://github.com/jcoiffin/Rx/releases/latest.  
In Windbg , load the extension using :  
.load C:\Temp\rxunzipped\rx.dll

Here are the available commands :  
!help       provide the help for the command lines  
!r c 		Runs a command line in R.  
!r s 		Runs a script in R.  
!r toR 	Runs a windbg command and store the result in R variable.  

# Example
You get an unstructure result from windbg commands which dumps objects as text.  
As a sample , this a windbg command parsing all threads waiting ldap response (in ldap_result_with_error function)  
and return the Domain Controller server used in these ldap requests :  
0:523> !mex.fel -x "~${@#Line} s;.frame3;du poi(poi(Connection)+0x1b0)" !mex.us -q Wldap32!ldap_result  
ntdll!ZwWaitForSingleObject+0xa:  
00007ff9`2c11071a c3              ret  
03 0000004d`f12cc9b0 00007ff9`29d067dc Wldap32!ldap_result_with_error+0x197  
0000004d`f93e1bb8  "serverDC06.mydomain.com"  
ntdll!ZwWaitForSingleObject+0xa:  
00007ff9`2c11071a c3              ret  
03 0000004d`f17ccaa0 00007ff9`29d067dc Wldap32!ldap_result_with_error+0x197  
0000004d`eb751028  "serverDC04.mydomain.com"  
ntdll!ZwWaitForSingleObject+0xa:  
00007ff9`2c11071a c3              ret  
...

We can save the result of this in R variable named DCnames using :  
0:523> !Rx.r toR CmdOutput !mex.fel -x "~${@#Line} s;.frame3;du poi(poi(Connection)+0x1b0)" !mex.us -q Wldap32!ldap_result  
Created R variable named CmdOutput  

We can use R to clean the data and extract the info we are interested in , in this case the Domain Controller server name.   
Below I use grep to only keep lines which contains double quotes , then sub to keep only the text between quotes.

0:523> !r c DCnames <- sub(".*\"(.*)\".*", "\\1", grep("\"",CmdOutput,value=TRUE))

Then you can do some statistics with the data.  
Here I will simply get the number of ldap requests per DC names  
0:523> !r c as.data.frame(table(DCnames))  
                     DCnames Freq  
1 serverDC01.mydomain.com    9  
2 serverDC02.mydomain.com    3  
3 serverDC03.mydomain.com    4  
4 serverDC04.mydomain.com   21  
5 serverDC05.mydomain.com    3  
6 serverDC06.mydomain.com    5  

Conclusion for this example may be that we are waiting mainly replies from server serverDC04.mydomain.com in our LDAP requests.  
In case the process are stuck on these ldap requests , performance may be tracked on this server DC04.


# !r c 
!r c <Rcommand> 
Runs a command line in R.

Example : 
 !r c "Hello from windbg" 

# !r toR <windbg command>
Runs a windbg command and store the result in R variable.

Example:  
0:523> !r toR lminR lm  
Created R variable named lminR  
0:523> !r c lminR  
This will dump the list of modules in lm which were stored in a R variable

# !r s
 !r s <ScriptName> [arguments] 
Runs a script in R.

In the example upper , you can create a script named findDCs.R in rxunzip folder with these lines :

#clean the data  
DCnames <- sub(".*\"(.*)\".*", "\\1", grep("\"",CmdOutput,value=TRUE))  
#display DC stats  
print(as.data.frame(table(DCnames)))  

Then use this script command to run R script :  
.load C:\Temp\rxunzipped\rx.dll  
!Rx.r toR CmdOutput !mex.fel -x "~${@#Line} s;.frame3;du poi(poi(Connection)+0x1b0)"  !mex.us -q Wldap32!ldap_result  
!r s findDCs.R 