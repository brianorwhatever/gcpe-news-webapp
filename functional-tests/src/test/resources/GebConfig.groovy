/*
	This is the Geb configuration file.

	See: http://www.gebish.org/manual/current/#configuration
*/


import org.openqa.selenium.Dimension
import org.openqa.selenium.chrome.ChromeDriver
import org.openqa.selenium.firefox.FirefoxDriver
import org.openqa.selenium.remote.DesiredCapabilities

waiting {
	timeout = 15
	retryInterval = 0.25
}

atCheckWaiting = [15, 025]

environments {

	// run via “./gradlew chromeTest”
	// See: http://code.google.com/p/selenium/wiki/ChromeDriver
	chrome {
		driver = { new ChromeDriver() }
	}

	// run via “./gradlew firefoxTest”
	// See: http://code.google.com/p/selenium/wiki/FirefoxDriver
	firefox {
		driver = { new FirefoxDriver() }
	}
}

// To run the tests with all browsers just run:
//
// phantomJs --> “./gradlew phantomJsTest”   (headless)
// chrome    --> "./gradlew chromeTest"
// baseUrl = "https://dev.news.gov.bc.ca"
def env = System.getenv()
baseUrl = env['BASEURL']
if (!baseUrl) {
	baseUrl = "https://dev.news.gov.bc.ca"
}
 
 

baseNavigatorWaiting = true

println """
            .  .       .
            |  |     o |
;-. ,-: . , |  | ;-. . |-
| | | | |/  |  | | | | |
' ' `-` '   `--` ' ' ' `-'
--------------------------
"""
println "BaseURL: ${baseUrl}"
println "--------------------------"
reportsDir = "gebReports"
quitCachedDriverOnShutdown = true
