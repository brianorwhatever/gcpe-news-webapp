import geb.spock.GebReportingSpec
import java.util.List;
import java.util.Iterator

import org.openqa.selenium.*

import pages.app.HomePage

import spock.lang.Specification
import spock.lang.Title
import spock.lang.Narrative
import spock.lang.Unroll

@Title("Carousel URLs work")

@Narrative("""
As a user of Gov News when I go to the home page and click on the title or the 'read more' I am redirected to the correct page.
""")

class CarouselURLsWorkSpec extends GebReportingSpec {
	@Unroll

	/*	Given I am visiting BC Gov News site
		And I go to the Home Page (https://news.gov.bc.ca/ )
		When I click on the title or read more of any given slider
		Then I am redirected to the URL set for that slider
	*/

	def "Carousel URLs Work"() {
		given: "I am a user of Gov News"
	  
		when: "I go to the Home page"
			to HomePage

		then: "wait for the page to load"
			assert waitFor { $(By.id("carousel-holder")).displayed == true }
			String homeUrl = driver.getCurrentUrl();
		
		and: "I see the current slide"
			List<WebElement> homeBanners = driver.findElements(By.cssSelector("div[class='home-banner']"))
			int currentIndex = -1
			// find the displayed slide
			for (int i=0; i<=homeBanners.size()-1; i++)
			{
				if (homeBanners[i].displayed == true)	
				{
					 currentIndex = i;
				}
			}
		and: "I copy the url of the slider for later testing"
			def attrib = homeBanners[currentIndex].findElement(By.xpath(".//div/div/div/div")).getAttribute("onClick")

			// split the list on the commas...the entry we want is the last one.
			List<String> vals = attrib.split("\\;")

			//now extract the URL
			String text = vals[vals.size()-1]
			
			String slideUrl = text.substring(text.indexOf("www"), text.length()-1)
			
		when: "I click on the heading"
			driver.findElement(By.xpath("//*[@id='carousel-holder']/div[1]/div[1]/div/div[1]/div[1]/h2")).click()

			// wait for the page to load
			waitFor { homeUrl != driver.getCurrentUrl() }
		
			text = driver.getCurrentUrl()
			String temptext = text.substring(text.indexOf("www"), text.length())
		
		then:
			assert slideUrl == temptext

		// now return to home page and do the same test clicking on 'read more'
		when: "I go return to the previous page"
			driver.navigate().back()

		then: "wait for the page to load"
			assert waitFor { $(By.id("carousel-holder")).displayed == true }

		when: "I click on the 'read more' link"
			driver.findElement(By.xpath("//*[@id='carousel-holder']/div[1]/div[1]/div/div[1]/div[1]/div/a")).click()

			// wait for the page to load
			waitFor { homeUrl != driver.getCurrentUrl() }
		
			text = driver.getCurrentUrl()
			temptext = text.substring(text.indexOf("www"), text.length())

		then:
			assert slideUrl == temptext
	}
}
	
