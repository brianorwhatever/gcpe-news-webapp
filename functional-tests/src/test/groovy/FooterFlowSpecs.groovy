import geb.spock.GebReportingSpec

import pages.app.ContactFacebookPage
import pages.app.ContactFlickrPage
import pages.app.ContactRSSFeedsPage
import pages.app.ContactTwitterPage
import pages.app.ContactYoutubePage
import pages.app.HomePage
import pages.app.LivePage
import pages.app.NewslettersPage
import pages.app.PrivacyPage
import pages.external.AccessibilityBCPage
import pages.external.ContactUsBCPage
import pages.external.CopyrightBCPage
import pages.external.DisclaimerBCPage
import pages.external.GovBCHomePage

import org.openqa.selenium.*

import spock.lang.Unroll
import spock.lang.Issue

class FooterFlowSpecs extends GebReportingSpec {

    @Unroll
 
		def "Navigate Page from: #startPage, click Link: #clickLink, Assert Page: #assertPage"(){
	    given: "I start on the #startPage"
			to startPage
			((JavascriptExecutor) driver).executeScript("window.scrollTo(0, document.body.scrollHeight)")
		
		when: "I click on the link #clickLink"
			waitFor { $("a", id:"$clickLink")[0].click() }
        then:
			at assertPage

        where:
        startPage			| clickLink				| clickCount    | timeoutSeconds    || assertPage
        HomePage            | "footerBCGov"			| 1             | 3                 || GovBCHomePage
		HomePage			| "footerFaceBook"		| 1             | 3					|| ContactFacebookPage
        HomePage            | "footerTwitter"		| 1             | 3                 || ContactTwitterPage
        HomePage            | "footerFlickr"		| 1             | 3                 || ContactFlickrPage
        HomePage            | "footerYoutube"		| 1             | 3                 || ContactYoutubePage
        HomePage            | "footerNewsletters"	| 1             | 3                 || NewslettersPage
        HomePage            | "footerRSSFeeds"		| 1             | 3                 || ContactRSSFeedsPage
        LivePage            | "footerHome"			| 1             | 3                 || HomePage
        HomePage            | "footerDisclaimer"	| 1             | 3                 || DisclaimerBCPage
        HomePage            | "footerPrivacy"		| 1             | 3                 || PrivacyPage
        HomePage            | "footerAccessibility"	| 1             | 3                 || AccessibilityBCPage
        HomePage            | "footerCopyright"		| 1             | 3                 || CopyrightBCPage
        HomePage            | "footerContact"		| 1             | 3                 || ContactUsBCPage
    }
}