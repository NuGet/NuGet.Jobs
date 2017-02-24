@echo OFF
	
cd bin

:Top
	echo "Starting job - NuGet - SupportRequests.Notifications.OnCallDailyNotification.cmd"
	
	title #{Jobs.supportrequests.notifications.Title}

	start /w nuget.supportrequests.notifications.exe -ConsoleLogOnly -Task "OnCallDailyNotification" -SourceDatabase "Server=tcp:vz2xmz8oda.database.windows.net;Database=nuget-prod-supportrequest;Persist Security Info=False;User ID=$$Prod-SupportRequestDBReadOnly-UserName$$;Password=$$Prod-SupportRequestDBReadOnly-Password$$;Connect Timeout=30;Encrypt=True" -PagerDutyAccountName "nuget" -PagerDutyApiKey "$$Prod-PagerDuty-ApiKey$$" -SmtpUri "smtps://nuget:$$Prod-SendGridSMTP-Password$$@smtp.sendgrid.net:587/" -VaultName "#{Deployment.Azure.KeyVault.VaultName}" -ClientId "#{Deployment.Azure.KeyVault.ClientId}" -CertificateThumbprint "#{Deployment.Azure.KeyVault.CertificateThumbprint}" -InstrumentationKey "#{Jobs.supportrequests.notifications.InstrumentationKey}" -verbose true -Once

	echo "Finished job - NuGet - SupportRequests.Notifications.OnCallDailyNotification.cmd"

	goto Top