import geb.spock.GebReportingSpec
import java.util.List;
import java.util.Iterator

import org.openqa.selenium.*

import pages.app.HomePage

import spock.lang.Specification
import spock.lang.Title
import spock.lang.Narrative
import spock.lang.Unroll

@Title("Carousel Next/Previous")

@Narrative("""
As a user of Gov News when I go to the home page and click on the next/previous arrows the next or previous slide is displayed.
""")

class CarouselNextPrevSpec extends GebReportingSpec {
	@Unroll

	/*	Given I am a user of Gov News website
		When I go to the Home page
		When I click on the Next slide arrow (right side)
		Then next slide is displayed (image and content)
		And I click on the Previous slide arrow (left side)
		Then the same slide is displayed as was showing when I first loaded the home page.
	*/

	def "Carousel Next Previous"() {
		given: "I am a user of Gov News"
	  
		when: "I go to the Home page"
			to HomePage

		then: "wait for the page to load"
			assert waitFor { $(By.id("carousel-holder")).displayed == true }

		and: "I see the current slide"
			List<WebElement> homeBanners = driver.findElements(By.cssSelector("div[class='home-banner']"))
			def banner1Name = ""
			def banner2Name = ""
			// find the displayed slide
			for (int i=0; i<=homeBanners.size()-1; i++)
			{
				if (homeBanners[i].displayed == true)
				{
					 banner1Name = homeBanners[i].getCssValue("background-image")			
				}
			}

			println("---------------")
			println (banner1Name )
			println("---------------")

		when: "I click on the 'next' arrow (right side)"
			$("#carousel-holder").find("div", class: "story-nav story-next").first().click()
			sleep(1000)

			def x = -1
			// find the displayed slide
			for (int i=0; i<=homeBanners.size()-1; i++)
			{
				if (homeBanners[i].displayed == true)
				{
					banner2Name = homeBanners[i].getCssValue("background-image")
					x = i
				}
			}

			println("***************")
			println (banner2Name)
			println("***************")

		then:
			assert waitFor { banner1Name !=  banner2Name }

		when: "I click on the 'preious' arrow (left side)"
			// we already know which slide is displayed so click its left arrow
			def leftArrows = driver.findElements(By.cssSelector("div[class^='story-nav story-previous']"))    
			leftArrows[x].click();

			sleep(1000)

			// find the displayed slide
			for (int y=0; y<=homeBanners.size()-1; y++)
			{
				if (homeBanners[y].displayed == true)
				{
					banner2Name = homeBanners[y].getCssValue("background-image")
				}
			}

			println("---------------")
			println (banner2Name)
			println("---------------")
	
		then:
			assert waitFor { banner1Name ==  banner2Name }
	}
}
	
