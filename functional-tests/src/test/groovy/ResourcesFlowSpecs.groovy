import geb.spock.GebReportingSpec

import pages.app.FactsheetPage
import pages.app.HomePage
import pages.app.MediaRelationsContactsPage
import pages.app.NewslettersPage
import pages.app.OfficeOfPremierPage
import pages.app.SubscribePage
import pages.external.ContactMLAPage
import pages.external.DataBCPage
import pages.external.EmergencyInfoBCPage
import pages.external.GovDirectoryBCPage
import pages.external.LegislativeAssemblyBCPage
import pages.external.NewsArchivePage
import pages.external.StatsBCPage

import org.openqa.selenium.*

import spock.lang.Unroll
import spock.lang.Issue

class ResourcesFlowSpecs extends GebReportingSpec {

    @Unroll
 
		def "Navigate Page from: #startPage, click Link: #clickLink, Assert Page: #assertPage"(){
	    given: "I start on the #startPage"
			to startPage
			((JavascriptExecutor) driver).executeScript("scroll(0,400);")
        when: "I click on the link #clickLink"
			waitFor { $("a", id:"$clickLink")[0].click() }
        then:
			at assertPage

        where:
        startPage			| clickLink				| clickCount    | timeoutSeconds    || assertPage
        HomePage            | "Resource_0"			| 1             | 3                 || OfficeOfPremierPage
		HomePage			| "Resource_1"			| 1             | 3					|| MediaRelationsContactsPage
        HomePage            | "Resource_2"			| 1             | 3                 || FactsheetPage
        HomePage            | "Resource_3"			| 1             | 3                 || NewslettersPage
        HomePage            | "Resource_4"			| 1             | 3                 || EmergencyInfoBCPage
        HomePage            | "Resource_5"			| 1             | 3                 || DataBCPage
        HomePage            | "Resource_6"			| 1             | 3                 || StatsBCPage
// Removed Jan 25, 2018: they pass in windows but are failing on the server in the pipeline
// Resource_7 removed because its title can't be validated, it contains crlfs and tabs which cause problems.
//        HomePage            | "Resource_7"			| 1             | 3                 || LegislativeAssemblyBCPage
// Resource_8 removed because navigation to the page is not allowed from host where the tests are being run in the pipeline?!
//        HomePage            | "Resource_8"			| 1             | 3                 || GovDirectoryBCPage
        HomePage            | "Resource_9"			| 1             | 3                 || ContactMLAPage
        HomePage            | "Resource_10"			| 1             | 3                 || NewsArchivePage
        HomePage            | "Resource_11"			| 1             | 3                 || SubscribePage

    }
}