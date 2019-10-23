// -*- groovy -*-

pipeline {
    agent {
        label {
            label 'windows-2019'
        }
    }
    options {
        timestamps()
        buildDiscarder(logRotator(numToKeepStr: '10'))
        timeout(time: 1, unit: 'HOURS')
    }
    stages {
        stage('Build') {
            steps {
                bat 'docker run --rm -v %cd%:C:/work -w C:/work/src mcr.microsoft.com/dotnet/framework/sdk:4.8-20190910-windowsservercore-ltsc2019 powershell -File build.ps1 -Target Build'
            }
        }
        stage('Test') {
            steps {
                bat 'docker run --rm -v %cd%:C:/work -w C:/work/src mcr.microsoft.com/dotnet/framework/sdk:4.8-20190910-windowsservercore-ltsc2019 powershell -File build.ps1 -Target Test'
            }
        }
        stage('Nuget-pack') {
            steps {
                bat 'docker run --rm -v %cd%:C:/work -w C:/work/src mcr.microsoft.com/dotnet/framework/sdk:4.8-20190910-windowsservercore-ltsc2019 powershell -File build.ps1 -Target Nuget-pack'
            }
        }
        stage('Nuget-push') {
            when { tag "*" }
            environment {
              NugetAPIKey = credentials('nuget-api-key')
            }
            steps {
                bat 'docker run --rm -v %cd%:C:/work -w C:/work/src -e NugetAPIKey -e TAG_NAME mcr.microsoft.com/dotnet/framework/sdk:4.8-20190910-windowsservercore-ltsc2019 powershell -File build.ps1 -Target Nuget-push'
            }
        }
    }
}
