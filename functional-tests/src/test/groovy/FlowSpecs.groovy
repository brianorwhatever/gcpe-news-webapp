import geb.spock.GebReportingSpec

import pages.app.ConnectPage
import pages.app.HomePage
import pages.app.LivePage
import pages.app.SectorsPage
import pages.app.SubscribePage
import pages.external.GovBCHomePage
import pages.external.NewsArchivePage

import spock.lang.Unroll
import org.openqa.selenium.*

class FlowSpecs extends GebReportingSpec {

	@Unroll

	def "BCGovNews test"(){
	    given: "I start on the Sectors page"
			to SectorsPage
		
		when: "I click on the link BC Gov News the top left"
			waitFor { $("a", id:"headerNewsHome").click() }
        then:
			at HomePage	
	}

	def "BritishColumbia test"(){
	    given: "I start on the home page"
			to HomePage
		
		when: "I click on the link provincial logo in the top left"
			waitFor { $("a", id:"headerBCGov").click() }
        then:
			at GovBCHomePage
	}

	def "HomeClick test"(){
	given: 
		to SectorsPage
		at SectorsPage
	when:
		$("a", text: "Home").first().click()
	then:
		at HomePage
	}

	def "MinistriesClick test"(){
	given:
		to HomePage
		assert waitFor { $("div", id: "carousel-holder").displayed == true } 
	when:
		$("a", text: "Ministries").click()
	then:
		assert waitFor { $(By.id("ministries-menu")).displayed == true }
	}

	def "SectorsClick test"(){
	given:
		to HomePage
		assert waitFor { $("div", id: "carousel-holder").displayed == true } 
	when:
		$("a", text: "Sectors").click()
	then:
		at SectorsPage    
    }

	def "ConnectClick test"(){
	given:
		to HomePage
		assert waitFor { $("div", id: "carousel-holder").displayed == true } 
	when:
		$("a", text: "Connect").click()
	then:
		at ConnectPage    
    }

	def "SubscribeClick test"(){
	given:
		to HomePage
		assert waitFor { $("div", id: "carousel-holder").displayed == true } 
	when:
		$("a", text: "Subscribe").first().click()
	then:
		at SubscribePage
	}

	def "NewsArchiveClick test"(){
	given:
		to HomePage
		assert waitFor { $("div", id: "carousel-holder").displayed == true } 
	when:
		$("a", text: "News Archive").first().click()
	then:
		at NewsArchivePage
	}

	def "LiveClick test"() {
	given:
		to HomePage
		assert waitFor { $("div", id: "carousel-holder").displayed == true } 
	when:
		$("a", text: "Live Webcast").click()
	then:
		at LivePage	
	}
}
